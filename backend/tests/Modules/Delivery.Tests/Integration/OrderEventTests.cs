using AwesomeAssertions;
using FreshOrbit.Delivery.Application.Handlers;
using FreshOrbit.Delivery.Domain.Shipments;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Tests.Support;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Delivery.Tests.Integration;

/// <summary>The handler services Ordering's events are mapped to (the dispatcher's save is simulated by <see cref="DeliveryHarness.HandleAsync"/>).</summary>
public sealed class OrderEventTests(DeliveryDatabase database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task I6_duplicate_OrderPlaced_creates_one_shipment()
    {
        var now = DeliveryHarness.Now();
        await using var harness = new DeliveryHarness(database, now);
        var location = harness.Locations.AddLocation();
        var slot = await harness.AddSlotAsync(location.Id, now.AddHours(6), booked: 1);
        var orderId = Guid.CreateVersion7();

        for (var i = 0; i < 2; i++)
        {
            await harness.HandleAsync(sp => sp.GetRequiredService<OrderEventHandlers>().OnOrderPlacedAsync(orderId, location.Id, slot.Id, now, Ct));
        }

        (await ShipmentsOfAsync(orderId)).Should().ContainSingle().Which.Status.Should().Be(ShipmentStatus.Pending);
    }

    [Fact]
    public async Task I7_duplicate_OrderCancelled_releases_the_slot_once()
    {
        var now = DeliveryHarness.Now();
        await using var harness = new DeliveryHarness(database, now);
        var location = harness.Locations.AddLocation();
        var slot = await harness.AddSlotAsync(location.Id, now.AddHours(6), booked: 2);
        var orderId = Guid.CreateVersion7();
        await harness.HandleAsync(sp => sp.GetRequiredService<OrderEventHandlers>().OnOrderPlacedAsync(orderId, location.Id, slot.Id, now, Ct));

        for (var i = 0; i < 2; i++)
        {
            await harness.HandleAsync(sp => sp.GetRequiredService<OrderEventHandlers>().OnOrderCancelledAsync(orderId, location.Id, slot.Id, now, Ct));
        }

        (await ShipmentsOfAsync(orderId)).Single().Status.Should().Be(ShipmentStatus.Cancelled);
        (await harness.FindAsync<DeliverySlot>(slot.Id))!.BookedCount.Should().Be(1);
    }

    [Fact]
    public async Task I8_OrderCancelled_before_OrderPlaced_cancels_and_releases_once()
    {
        var now = DeliveryHarness.Now();
        await using var harness = new DeliveryHarness(database, now);
        var location = harness.Locations.AddLocation();
        var slot = await harness.AddSlotAsync(location.Id, now.AddHours(6), booked: 1);
        var orderId = Guid.CreateVersion7();

        await harness.HandleAsync(sp => sp.GetRequiredService<OrderEventHandlers>().OnOrderCancelledAsync(orderId, location.Id, slot.Id, now, Ct));
        await harness.HandleAsync(sp => sp.GetRequiredService<OrderEventHandlers>().OnOrderPlacedAsync(orderId, location.Id, slot.Id, now, Ct));

        (await ShipmentsOfAsync(orderId)).Should().ContainSingle().Which.Status.Should().Be(ShipmentStatus.Cancelled);
        (await harness.FindAsync<DeliverySlot>(slot.Id))!.BookedCount.Should().Be(0);
    }

    [Fact]
    public async Task OrderPaymentFailed_cancels_a_pending_shipment_and_releases_the_slot()
    {
        var now = DeliveryHarness.Now();
        await using var harness = new DeliveryHarness(database, now);
        var location = harness.Locations.AddLocation();
        var slot = await harness.AddSlotAsync(location.Id, now.AddHours(6), booked: 1);
        var orderId = Guid.CreateVersion7();
        await harness.HandleAsync(sp => sp.GetRequiredService<OrderEventHandlers>().OnOrderPlacedAsync(orderId, location.Id, slot.Id, now, Ct));

        await harness.HandleAsync(sp => sp.GetRequiredService<OrderEventHandlers>().OnOrderPaymentFailedAsync(orderId, location.Id, slot.Id, now, Ct));

        (await ShipmentsOfAsync(orderId)).Single().Status.Should().Be(ShipmentStatus.Cancelled);
        (await harness.FindAsync<DeliverySlot>(slot.Id))!.BookedCount.Should().Be(0);
    }

    [Fact]
    public async Task OrderPacked_sets_packed_at_once_and_waits_for_the_shipment()
    {
        var now = DeliveryHarness.Now();
        await using var harness = new DeliveryHarness(database, now);
        var location = harness.Locations.AddLocation();
        var slot = await harness.AddSlotAsync(location.Id, now.AddHours(6), booked: 1);
        var orderId = Guid.CreateVersion7();

        var early = () => harness.HandleAsync(sp => sp.GetRequiredService<OrderEventHandlers>().OnOrderPackedAsync(orderId, now, true, Ct));
        await early.Should().ThrowAsync<InvalidOperationException>();

        await harness.HandleAsync(sp => sp.GetRequiredService<OrderEventHandlers>().OnOrderPlacedAsync(orderId, location.Id, slot.Id, now, Ct));
        await harness.HandleAsync(sp => sp.GetRequiredService<OrderEventHandlers>().OnOrderPackedAsync(orderId, now, true, Ct));
        await harness.HandleAsync(sp => sp.GetRequiredService<OrderEventHandlers>().OnOrderPackedAsync(orderId, now.AddMinutes(5), false, Ct));

        var shipment = (await ShipmentsOfAsync(orderId)).Single();
        shipment.PackedAt.Should().Be(now);
        shipment.ColdBoxUsed.Should().BeTrue();
    }

    private async Task<List<Shipment>> ShipmentsOfAsync(Guid orderId)
    {
        await using FreshOrbitDbContext db = database.CreateDbContext();
        return await db.Set<Shipment>().AsNoTracking().Where(s => s.OrderId == orderId).ToListAsync(Ct);
    }
}
