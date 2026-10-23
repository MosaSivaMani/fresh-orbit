using AwesomeAssertions;
using FreshOrbit.Inventory.Application.Jobs;
using FreshOrbit.Inventory.Domain.ColdChain;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Infrastructure.Seeding;
using FreshOrbit.Inventory.Tests.Support;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Inventory.Tests.Integration;

/// <summary>Recurring jobs (inventory.md §5) and the reference-data loader.</summary>
public sealed class JobTests(InventoryDatabase database) : InventoryTestBase(database)
{
    [Fact]
    public async Task I11_I12_expiry_writes_off_past_best_before_batches_once()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: [new SeedBatch(400, 40_000, TimeSpan.FromMinutes(-1), "OLD"), new SeedBatch(600, 60_000, TimeSpan.FromDays(2), "NEW")]);
        await using var services = CreateServices();

        int first, second;
        await using (var scope = services.CreateAsyncScope())
        {
            first = await scope.ServiceProvider.GetRequiredService<BatchExpiryService>().RunAsync(Ct);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            second = await scope.ServiceProvider.GetRequiredService<BatchExpiryService>().RunAsync(Ct);
        }

        first.Should().BeGreaterThanOrEqualTo(1);
        second.Should().Be(0);
        var saved = await LoadStockItemAsync(item.Id);
        saved.Batches.Single(b => b.BatchCode == "OLD").Status.Should().Be(BatchStatus.Expired);
        saved.OnHand.Should().Be(600);
        var waste = (await LoadMovementsAsync(item.Id)).Should().ContainSingle(m => m.Kind == MovementKind.Waste).Subject;
        (waste.Quantity, waste.CostPaise, waste.ReasonCode, waste.ReferenceType).Should().Be((-400, -40_000L, "EXPIRED", MovementReferenceType.Batch));
        (await OutboxAsync("BatchExpired", item.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task Low_stock_digest_is_raised_once_per_location_in_the_local_7_oclock_hour()
    {
        var location = Locations.AddLocation();
        var item = await SeedStockItemAsync(location, Catalog.AddVariant(), threshold: 1000, batches: new SeedBatch(800, 80_000, TimeSpan.FromDays(30)));
        await using var services = CreateServices();

        Clock.Set(new DateTimeOffset(2026, 11, 2, 1, 0, 0, TimeSpan.Zero)); // 06:30 IST: not the digest hour
        var early = await RunDigestAsync(services);
        Clock.Set(new DateTimeOffset(2026, 11, 2, 2, 0, 0, TimeSpan.Zero)); // 07:30 IST
        var due = await RunDigestAsync(services);
        var repeat = await RunDigestAsync(services);

        (early, due, repeat).Should().Be((0, 1, 0));
        var digest = (await OutboxAsync("LowStockDigestReady", location)).Should().ContainSingle().Subject;
        digest.GetProperty("items")[0].GetProperty("stockItemId").GetGuid().Should().Be(item.Id);
        digest.GetProperty("items")[0].GetProperty("available").GetInt32().Should().Be(800);
    }

    [Fact]
    public async Task Storage_unit_ranges_are_loaded_from_reference_data_idempotently()
    {
        await using (var db = CreateDbContext())
        {
            await new StorageUnitsReferenceDataLoader(db).LoadAsync(new ReferenceDataContext(InventoryDatabase.ReferenceDataPath), Ct);
        }

        await using var read = CreateDbContext();
        var ranges = await read.Set<StorageUnitRange>().AsNoTracking().OrderBy(r => r.Code).ToListAsync(Ct);
        ranges.Select(r => (r.Code, r.MinC, r.MaxC)).Should().Equal(("CHILLER", -1.0m, 4.0m), ("FREEZER", null, -18.0m), ("RECEIPT", null, 4.0m));
    }

    [Fact]
    public void Jobs_are_registered_with_their_schedules()
    {
        using var services = CreateServices();

        var jobs = services.GetServices<IRecurringJobRegistration>().SelectMany(r => r.GetJobs()).ToList();

        jobs.Select(j => (j.Id, j.Cron)).Should().BeEquivalentTo([("inventory.expire-batches", "*/15 * * * *"), ("inventory.low-stock-digest", "0 * * * *")]);
        using var scope = services.CreateScope();
        jobs.Should().OnlyContain(j => scope.ServiceProvider.GetRequiredService(j.JobType) is IRecurringJob);
    }

    private static async Task<int> RunDigestAsync(ServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LowStockDigestService>().RunAsync(Ct);
    }
}
