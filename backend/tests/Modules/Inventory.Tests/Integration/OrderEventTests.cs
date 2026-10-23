using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Inventory.Application.Handlers;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Inventory.Tests.Support;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Events;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Inventory.Tests.Integration;

/// <summary>Test-only stand-in for Ordering's OrderPacked event, to drive the handler through the real outbox dispatcher.</summary>
public sealed record TestOrderPacked(Guid OrderId, Guid FulfillmentLocationId, DateTimeOffset PackedAt, IReadOnlyList<TestPackedLine> Lines) : DomainEvent;

public sealed record TestPackedLine(Guid OrderItemId, Guid VariantId, int YieldBp, int PackedQuantity, string Status);

/// <summary>The thin adapter shape the manager adds at merge (here for the test-only event).</summary>
public sealed class TestOrderPackedHandler(OrderStockHandlers handlers) : IEventHandler<TestOrderPacked>
{
    public Task HandleAsync(TestOrderPacked domainEvent, CancellationToken cancellationToken)
        => handlers.ConsumeForPackedOrderAsync(
            new PackedOrder(
                domainEvent.OrderId,
                domainEvent.FulfillmentLocationId,
                domainEvent.PackedAt,
                [.. domainEvent.Lines.Select(l => new PackedLine(l.OrderItemId, l.VariantId, l.YieldBp, l.PackedQuantity, SharedKernel.Codes.EnumCodes.ToEnum<PackedLineStatus>(l.Status)))]),
            cancellationToken);
}

/// <summary>Inventory's reactions to Ordering events (inventory.md §3), driven through the handler services.</summary>
public sealed class OrderEventTests(InventoryDatabase database) : InventoryTestBase(database)
{
    [Fact]
    public async Task I3_pack_consumes_the_reservation_and_sells_FEFO_across_two_batches_with_cost_remainder()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: [new SeedBatch(300, 30_001, TimeSpan.FromDays(1), "A"), new SeedBatch(1000, 84_500, TimeSpan.FromDays(2), "B")]);
        await using var services = CreateServices();
        var (orderId, orderItemId) = await ReserveAsync(services, location, variant, GrossQuantity.From(QuantityUnit.Gram, 500, 6500));

        await HandleAsync(services, h => h.ConsumeForPackedOrderAsync(Packed(orderId, location, (orderItemId, variant, 6500, 500, PackedLineStatus.Active)), Ct));

        var saved = await LoadStockItemAsync(item.Id);
        var a = saved.Batches.Single(b => b.BatchCode == "A");
        var b = saved.Batches.Single(b => b.BatchCode == "B");
        var sales = (await LoadMovementsAsync(item.Id)).Where(m => m.Kind == MovementKind.Sale).ToList();
        sales.Select(m => (m.BatchId, m.Quantity, m.CostPaise, m.ReferenceType, m.ReferenceId)).Should().BeEquivalentTo(
        [
            ((Guid?)a.Id, -300, -30_001L, MovementReferenceType.OrderItem, (Guid?)orderItemId),
            ((Guid?)b.Id, -470, -39_715L, MovementReferenceType.OrderItem, (Guid?)orderItemId),
        ]);
        a.Status.Should().Be(BatchStatus.Depleted);
        b.RemainingQuantity.Should().Be(530);
        saved.OnHand.Should().Be(530).And.Be(saved.Batches.Where(x => x.Status == BatchStatus.Active).Sum(x => x.RemainingQuantity));
        saved.Reserved.Should().Be(0);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Consumed);
    }

    [Fact]
    public async Task Pack_overweight_beyond_free_stock_records_the_fact_and_raises_StockOverCommitted()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(1000, 100_000, TimeSpan.FromDays(2)));
        await using var services = CreateServices();
        var (orderId, orderItemId) = await ReserveAsync(services, location, variant, 500);
        await ReserveAsync(services, location, variant, 450);

        await HandleAsync(services, h => h.ConsumeForPackedOrderAsync(Packed(orderId, location, (orderItemId, variant, 10000, 580, PackedLineStatus.Active)), Ct));

        var saved = await LoadStockItemAsync(item.Id);
        (saved.OnHand, saved.Reserved).Should().Be((420, 450));
        (await OutboxAsync("StockOverCommitted", item.Id)).Should().ContainSingle().Which.GetProperty("shortfall").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task Short_supplied_and_substituted_lines_release_without_a_sale()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(1000, 100_000, TimeSpan.FromDays(2)));
        await using var services = CreateServices();
        var (orderId, shortItem) = await ReserveAsync(services, location, variant, 300);
        var zeroItem = await ReserveForOrderAsync(services, orderId, location, variant, 200);

        await HandleAsync(services, h => h.ConsumeForPackedOrderAsync(
            Packed(orderId, location, (shortItem, variant, 10000, 0, PackedLineStatus.ShortSupplied), (zeroItem, variant, 10000, 0, PackedLineStatus.Active)), Ct));

        var saved = await LoadStockItemAsync(item.Id);
        saved.Reserved.Should().Be(0);
        saved.OnHand.Should().Be(1000);
        saved.Reservations.Should().OnlyContain(r => r.Status == ReservationStatus.Released);
        (await LoadMovementsAsync(item.Id)).Should().NotContain(m => m.Kind == MovementKind.Sale);
    }

    [Fact]
    public async Task I5_duplicate_OrderPacked_delivery_sells_once_with_and_without_the_inbox_row()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(2000, 200_000, TimeSpan.FromDays(2)));
        await using var services = CreateServices();
        var (orderId, orderItemId) = await ReserveAsync(services, location, variant, 770);
        var packed = new TestOrderPacked(orderId, location, Clock.UtcNow, [new TestPackedLine(orderItemId, variant, 6500, 500, "ACTIVE")]);
        await using var dispatcherServices = Database.CreateServices(
            s =>
            {
                new Infrastructure.InventoryModule().Register(s, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
                s.AddEventHandler<TestOrderPacked, TestOrderPackedHandler>();
            },
            Clock);
        await using (var db = CreateDbContext())
        {
            db.Add(new OutboxMessage(packed.EventId, EventTypeNames.Format(typeof(TestOrderPacked)), JsonSerializer.Serialize(packed, FreshOrbitJson.Options), packed.OccurredAt, null));
            await db.SaveChangesAsync(Ct);
        }

        var dispatcher = dispatcherServices.GetRequiredService<OutboxDispatcher>();
        await DispatchAllAsync(dispatcher);
        await RedeliverAsync(packed.EventId, removeInbox: false);
        await DispatchAllAsync(dispatcher);
        await RedeliverAsync(packed.EventId, removeInbox: true);
        await DispatchAllAsync(dispatcher);

        var saved = await LoadStockItemAsync(item.Id);
        (await LoadMovementsAsync(item.Id)).Where(m => m.Kind == MovementKind.Sale).Sum(m => m.Quantity).Should().Be(-770);
        saved.OnHand.Should().Be(1230);
        saved.Reserved.Should().Be(0);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Consumed);
    }

    [Fact]
    public async Task I6_duplicate_OrderCancelled_releases_once()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(2000, 200_000, TimeSpan.FromDays(2)));
        await using var services = CreateServices();
        var (orderId, _) = await ReserveAsync(services, location, variant, 400);
        await ReserveAsync(services, location, variant, 300);

        await HandleAsync(services, h => h.ReleaseReservationsAsync(orderId, Ct));
        await HandleAsync(services, h => h.ReleaseReservationsAsync(orderId, Ct));

        var saved = await LoadStockItemAsync(item.Id);
        saved.Reserved.Should().Be(300);
        saved.Reservations.Where(r => r.OrderId == orderId).Should().ContainSingle().Which.Status.Should().Be(ReservationStatus.Released);
    }

    [Fact]
    public async Task I13_OrderPaymentFailed_expires_reservations_and_restores_reserved()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(2000, 200_000, TimeSpan.FromDays(2)));
        await using var services = CreateServices();
        var (orderId, _) = await ReserveAsync(services, location, variant, 400, Clock.UtcNow.AddMinutes(15));

        await HandleAsync(services, h => h.ExpireReservationsAsync(orderId, Ct));

        var saved = await LoadStockItemAsync(item.Id);
        saved.Reserved.Should().Be(0);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Expired);
    }

    [Fact]
    public async Task OrderConfirmed_clears_the_expiry_and_moves_the_row_version()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(2000, 200_000, TimeSpan.FromDays(2)));
        await using var services = CreateServices();
        var (orderId, _) = await ReserveAsync(services, location, variant, 400, Clock.UtcNow.AddMinutes(15));
        var before = (await LoadStockItemAsync(item.Id)).Version;

        await HandleAsync(services, h => h.ConfirmReservationsAsync(orderId, Ct));
        await HandleAsync(services, h => h.ConfirmReservationsAsync(orderId, Ct));

        var saved = await LoadStockItemAsync(item.Id);
        saved.Reservations.Single().ExpiresAt.Should().BeNull();
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
        saved.Version.Should().NotBe(before);
    }

    private static PackedOrder Packed(Guid orderId, Guid location, params (Guid OrderItemId, Guid VariantId, int YieldBp, int Packed, PackedLineStatus Status)[] lines)
        => new(orderId, location, DateTimeOffset.UtcNow, [.. lines.Select(l => new PackedLine(l.OrderItemId, l.VariantId, l.YieldBp, l.Packed, l.Status))]);

    private static async Task<(Guid OrderId, Guid OrderItemId)> ReserveAsync(ServiceProvider services, Guid location, Guid variant, int gross, DateTimeOffset? expiresAt = null)
    {
        var orderId = Guid.CreateVersion7();
        return (orderId, await ReserveForOrderAsync(services, orderId, location, variant, gross, expiresAt));
    }

    private static async Task<Guid> ReserveForOrderAsync(ServiceProvider services, Guid orderId, Guid location, Guid variant, int gross, DateTimeOffset? expiresAt = null)
    {
        var orderItemId = Guid.CreateVersion7();
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IInventoryApi>().ReserveAsync(new ReserveStockRequest(orderId, location, [new(orderItemId, variant, gross)], expiresAt), Ct);
        result.Succeeded.Should().BeTrue();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return orderItemId;
    }

    /// <summary>Runs a handler method in its own scope and saves its tracked changes, as the outbox dispatcher does.</summary>
    private static async Task HandleAsync(ServiceProvider services, Func<OrderStockHandlers, Task> handle)
    {
        await using var scope = services.CreateAsyncScope();
        await handle(scope.ServiceProvider.GetRequiredService<OrderStockHandlers>());
        await scope.ServiceProvider.GetRequiredService<FreshOrbitDbContext>().SaveChangesAsync(Ct);
    }

    private static async Task DispatchAllAsync(OutboxDispatcher dispatcher)
    {
        while (await dispatcher.DispatchBatchAsync(Ct) > 0)
        {
        }
    }

    private async Task RedeliverAsync(Guid eventId, bool removeInbox)
    {
        await using var db = CreateDbContext();
        await db.Set<OutboxMessage>().Where(m => m.Id == eventId).ExecuteUpdateAsync(s => s.SetProperty(m => m.ProcessedAt, (DateTimeOffset?)null), Ct);
        if (removeInbox)
        {
            await db.Set<InboxRecord>().Where(i => i.EventId == eventId).ExecuteDeleteAsync(Ct);
        }
    }
}
