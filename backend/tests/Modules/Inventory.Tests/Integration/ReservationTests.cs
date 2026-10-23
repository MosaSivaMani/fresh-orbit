using AwesomeAssertions;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Inventory.Tests.Support;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Inventory.Tests.Integration;

/// <summary>Checkout reservation through Inventory.Public (INV-R19, R24), as Ordering will call it.</summary>
public sealed class ReservationTests(InventoryDatabase database) : InventoryTestBase(database)
{
    [Fact]
    public async Task I1_concurrent_checkouts_for_the_last_stock_exactly_one_wins()
    {
        var location = Locations.AddLocation();
        var variant = Catalog.AddVariant();
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(1000, 845_000, TimeSpan.FromDays(2)));
        await using var services = CreateServices();
        var bothRead = new Barrier(2);

        async Task<string> CheckoutAsync()
        {
            for (var attempt = 1; ; attempt++)
            {
                await using var scope = services.CreateAsyncScope();
                var inventory = scope.ServiceProvider.GetRequiredService<IInventoryApi>();
                var orderId = Guid.CreateVersion7();
                var result = await inventory.ReserveAsync(new ReserveStockRequest(orderId, location, [new ReserveStockLine(Guid.CreateVersion7(), variant, 770)], null), Ct);
                if (attempt == 1)
                {
                    // Both checkouts have read and decided before either saves: a real race on the row version.
                    await Task.Run(() => bothRead.SignalAndWait(TimeSpan.FromSeconds(10)), Ct);
                }

                if (!result.Succeeded)
                {
                    return result.Refusals.Single().Code;
                }

                try
                {
                    await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
                    return "RESERVED";
                }
                catch (ConcurrentSaveException) when (attempt < 3)
                {
                    // Lost race: retry the whole checkout from Read.
                }
            }
        }

        var outcomes = await Task.WhenAll(Task.Run(CheckoutAsync, Ct), Task.Run(CheckoutAsync, Ct));

        outcomes.Should().BeEquivalentTo(["RESERVED", "OUT_OF_STOCK"]);
        var saved = await LoadStockItemAsync(item.Id);
        saved.Reserved.Should().Be(770);
        saved.Reservations.Should().ContainSingle(r => r.Status == ReservationStatus.Active).Which.Quantity.Should().Be(770);
    }

    [Fact]
    public async Task I2_yield_adjusted_reservation_holds_the_gross_quantity()
    {
        var location = Locations.AddLocation();
        var variant = Catalog.AddVariant();
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(2000, 1_690_000, TimeSpan.FromDays(2)));
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var inventory = scope.ServiceProvider.GetRequiredService<IInventoryApi>();
        var gross = GrossQuantity.From(QuantityUnit.Gram, 500, 6500);

        var result = await inventory.ReserveAsync(new ReserveStockRequest(Guid.CreateVersion7(), location, [new ReserveStockLine(Guid.CreateVersion7(), variant, gross)], Clock.UtcNow.AddMinutes(15)), Ct);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        result.Succeeded.Should().BeTrue();
        var saved = await LoadStockItemAsync(item.Id);
        saved.Reservations.Single().Quantity.Should().Be(770);
        saved.Reserved.Should().Be(770);
        (await inventory.GetAvailabilityAsync(location, [variant], Ct)).Single().AvailableQuantity.Should().Be(2000 - 770);
    }

    [Fact]
    public async Task U6_reserve_is_all_or_nothing_across_lines()
    {
        var location = Locations.AddLocation();
        var plenty = Catalog.AddVariant();
        var scarce = Catalog.AddVariant();
        var missing = Catalog.AddVariant();
        var plentyItem = await SeedStockItemAsync(location, plenty, batches: new SeedBatch(5000, 100_000, TimeSpan.FromDays(2)));
        await SeedStockItemAsync(location, scarce, batches: new SeedBatch(100, 10_000, TimeSpan.FromDays(2)));
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var inventory = scope.ServiceProvider.GetRequiredService<IInventoryApi>();
        var lines = new[]
        {
            new ReserveStockLine(Guid.CreateVersion7(), plenty, 500),
            new ReserveStockLine(Guid.CreateVersion7(), scarce, 60),
            new ReserveStockLine(Guid.CreateVersion7(), scarce, 60),
            new ReserveStockLine(Guid.CreateVersion7(), missing, 1),
        };

        var result = await inventory.ReserveAsync(new ReserveStockRequest(Guid.CreateVersion7(), location, lines, null), Ct);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        result.Succeeded.Should().BeFalse();
        result.ShortVariantIds.Should().BeEquivalentTo([scarce, missing]);
        result.Refusals.Select(r => (r.OrderItemId, r.Code)).Should().BeEquivalentTo(
            [(lines[1].OrderItemId, "OUT_OF_STOCK"), (lines[2].OrderItemId, "OUT_OF_STOCK"), (lines[3].OrderItemId, "STOCK_ITEM_MISSING")]);
        (await LoadStockItemAsync(plentyItem.Id)).Reserved.Should().Be(0);
    }

    [Fact]
    public async Task Release_for_substitution_frees_the_reservation_of_the_item_only()
    {
        var location = Locations.AddLocation();
        var variant = Catalog.AddVariant();
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(2000, 100_000, TimeSpan.FromDays(2)));
        await using var services = CreateServices();
        var (kept, released) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await using (var scope = services.CreateAsyncScope())
        {
            var inventory = scope.ServiceProvider.GetRequiredService<IInventoryApi>();
            await inventory.ReserveAsync(new ReserveStockRequest(Guid.CreateVersion7(), location, [new(kept, variant, 300), new(released, variant, 400)], null), Ct);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IInventoryApi>().ReleaseAsync([released], Ct);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }

        var saved = await LoadStockItemAsync(item.Id);
        saved.Reserved.Should().Be(300);
        saved.Reservations.Single(r => r.OrderItemId == released).Status.Should().Be(ReservationStatus.Released);
    }

    [Fact]
    public async Task Order_stock_lists_reservations_and_sales_with_batch_codes()
    {
        var location = Locations.AddLocation();
        var variant = Catalog.AddVariant();
        await SeedStockItemAsync(location, variant, batches: new SeedBatch(1000, 100_000, TimeSpan.FromDays(2), "SEER-01"));
        await using var services = CreateServices();
        var (orderId, orderItemId) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IInventoryApi>().ReserveAsync(new ReserveStockRequest(orderId, location, [new(orderItemId, variant, 300)], null), Ct);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Application.Handlers.OrderStockHandlers>().ConsumeForPackedOrderAsync(
                new Application.Handlers.PackedOrder(orderId, location, Clock.UtcNow, [new(orderItemId, variant, 10000, 300, Application.Handlers.PackedLineStatus.Active)]),
                Ct);
            await scope.ServiceProvider.GetRequiredService<FreshOrbitDbContext>().SaveChangesAsync(Ct);
        }

        await using var read = services.CreateAsyncScope();
        var stock = await read.ServiceProvider.GetRequiredService<IInventoryApi>().GetOrderStockAsync(orderId, Ct);

        stock.Reservations.Should().ContainSingle(r => r.OrderItemId == orderItemId && r.Status == "CONSUMED" && r.Quantity == 300);
        stock.Sales.Should().ContainSingle(s => s.OrderItemId == orderItemId && s.BatchCode == "SEER-01" && s.Quantity == -300 && s.CostPaise == -30_000);
        (await read.ServiceProvider.GetRequiredService<IInventoryApi>().HasStockItemAsync(location, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task City_activation_check_reports_STOCK_ITEM_MISSING_until_a_location_of_the_city_has_a_stock_item()
    {
        var cityId = Guid.CreateVersion7();
        var location = Locations.AddLocation(cityId);
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var check = scope.ServiceProvider.GetServices<ICityActivationCheck>().Single();

        (await check.GetMissingItemsAsync(cityId, Ct)).Should().Equal("STOCK_ITEM_MISSING");
        await SeedStockItemAsync(location, Catalog.AddVariant());
        (await check.GetMissingItemsAsync(cityId, Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Database_refuses_a_second_active_reservation_for_one_order_item()
    {
        var location = Locations.AddLocation();
        var variant = Catalog.AddVariant();
        await SeedStockItemAsync(location, variant, batches: new SeedBatch(1000, 100_000, TimeSpan.FromDays(2)));
        await using var services = CreateServices();
        var orderItemId = Guid.CreateVersion7();
        for (var i = 0; i < 2; i++)
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IInventoryApi>().ReserveAsync(new ReserveStockRequest(Guid.CreateVersion7(), location, [new(orderItemId, variant, 100)], null), Ct);
            var save = () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
            if (i == 0)
            {
                await save();
            }
            else
            {
                await save.Should().ThrowAsync<DbUpdateException>();
            }
        }
    }
}
