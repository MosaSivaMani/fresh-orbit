using FreshOrbit.Cart.Public;
using FreshOrbit.Settings.Application.Numbering;
using FreshOrbit.Settings.Domain.BusinessProfiles;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Storage;
using FreshOrbit.SharedKernel.Web.Storage;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BusinessProfile = FreshOrbit.Settings.Public.BusinessProfile;
using DomainBusinessProfile = FreshOrbit.Settings.Domain.BusinessProfiles.BusinessProfile;
using DomainNumberSeries = FreshOrbit.Settings.Domain.Numbering.NumberSeries;

namespace FreshOrbit.Ordering.Tests.Integration;

/// <summary>The API host with the other modules' Public APIs replaced by <see cref="CheckoutWorld"/>.</summary>
public sealed class OrderingApiFactory(OrderingDatabase database, CheckoutWorld world) : ApiFactory(database)
{
    public CheckoutWorld World => world;

    /// <summary>Local-disk storage in a fresh temporary folder (complaint photos, W12).</summary>
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "freshorbit-ordering-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton(world);
        services.AddSingleton(database);
        services.Replace(ServiceDescriptor.Singleton(world.Customers));
        services.Replace(ServiceDescriptor.Singleton(world.Locations));
        services.Replace(ServiceDescriptor.Singleton(world.Delivery));
        services.Replace(ServiceDescriptor.Singleton(world.Inventory));
        services.Replace(ServiceDescriptor.Singleton(world.Payments));
        services.Replace(ServiceDescriptor.Singleton(world.Promotions));
        services.Replace(ServiceDescriptor.Singleton(world.Catalog));
        services.Replace(ServiceDescriptor.Singleton(world.Pricing));
        services.Replace(ServiceDescriptor.Scoped<ICartApi, FakeCartApi>());
        services.Replace(ServiceDescriptor.Scoped<ISettingsApi, WorldPolicySettings>());
        services.Replace(ServiceDescriptor.Singleton<IFileStorage>(FileStorageExtensions.CreateLocalDisk(StorageRoot, environmentName: "Testing")));
    }
}

/// <summary>
/// Cart as seen by checkout. <c>MarkConverted</c> touches a probe aggregate on the shared unit of work; while
/// <see cref="CheckoutWorld.RacesToLose"/> &gt; 0 another connection changes the probe first, so the checkout's single
/// save loses the race (ConcurrentSaveException) exactly like a concurrent cart change would.
/// </summary>
public sealed class FakeCartApi(CheckoutWorld world, FreshOrbitDbContext db, OrderingDatabase database) : ICartApi
{
    public Task<CheckoutCart?> GetCheckoutCartAsync(Guid customerId, CancellationToken cancellationToken = default)
        => Task.FromResult<CheckoutCart?>(world.Lines.Count == 0 ? null : world.CheckoutCart());

    public Task<CartQuote?> QuoteAsync(CartQuoteRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult<CartQuote?>(world.Lines.Count == 0 ? null : world.BuildQuote());

    public async Task MarkConvertedAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        world.MarkConvertedCalls++;
        var probe = await db.Set<Probe>().FirstAsync(p => p.Id == world.ProbeId, cancellationToken);
        probe.Touch();
        if (world.RacesToLose > 0)
        {
            world.RacesToLose--;
            await world.BumpProbeElsewhereAsync(database.ConnectionString);
        }
    }

    public Task<GuestCartMergeResult> MergeGuestCartAsync(string guestToken, Guid customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(new GuestCartMergeResult(null, 0, 0));

    public Task<CartView> AddLinesAsync(Guid customerId, string pincode, IReadOnlyList<CartLineToAdd> lines, CancellationToken cancellationToken = default)
    {
        world.ReorderedLines = lines;
        return Task.FromResult(world.BuildQuote().View);
    }
}

/// <summary>
/// Settings as seen by Ordering: policy and business profile from the world (tests change them per case); numbers from
/// the real Settings number series service, which locks the series row in the caller's transaction (rolled back with
/// it, so numbers stay gapless). The series and business profile rows are seeded by <see cref="SettingsSeed"/>.
/// </summary>
public sealed class WorldPolicySettings(CheckoutWorld world, INumberSeriesService numberSeries) : ISettingsApi
{
    public Task<OrderPolicy> GetOrderPolicyAsync(Guid? cityId, CancellationToken cancellationToken = default) => Task.FromResult(world.Policy);

    public Task<BusinessProfile> GetBusinessProfileAsync(CancellationToken cancellationToken = default) => Task.FromResult(world.Profile);

    /// <summary>The real Settings rule (D295) over the world's profile; tests clear a field to see checkout refused.</summary>
    public Task<StoreReadiness> GetStoreReadinessAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new StoreReadiness(InvoicingReadiness.MissingFields(world.Profile.LegalName, world.Profile.Gstin, world.Profile.TimeZone)));

    public Task<string> NextNumberAsync(string seriesCode, string period, CancellationToken cancellationToken = default)
        => numberSeries.NextNumberAsync(seriesCode, period, cancellationToken);

    public async Task<string> NextNumberAsync(string seriesCode, DateTimeOffset at, CancellationToken cancellationToken = default)
        => await numberSeries.NextNumberAsync(seriesCode, await numberSeries.PeriodForAsync(at, cancellationToken), cancellationToken);

    public Task<string> GetFinancialPeriodAsync(DateTimeOffset at, CancellationToken cancellationToken = default)
        => numberSeries.PeriodForAsync(at, cancellationToken);
}

/// <summary>Seeds the Settings rows the real number series needs (business profile time zone, ORDER and INVOICE series).</summary>
public static class SettingsSeed
{
    public const string Period = "2026-27";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task EnsureAsync(FreshOrbitDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (!await db.Set<DomainBusinessProfile>().AnyAsync(cancellationToken))
            {
                db.Add(DomainBusinessProfile.Create(new BusinessProfileData(
                    "Fresh Orbit Foods Pvt Ltd", "Fresh Orbit", null, null, "Chennai", "+914400000000", "care@example.com", "Asia/Kolkata")));
            }

            if (!await db.Set<DomainNumberSeries>().AnyAsync(cancellationToken))
            {
                db.Add(DomainNumberSeries.Create("ORDER", Period, "FO-26", 4, 1));
                db.Add(DomainNumberSeries.Create("INVOICE", Period, "INV/26-27/", 6, 1));
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }
}

public static class NumberSeries
{
    /// <summary>The next value the Settings series will issue.</summary>
    public static async Task<long> PeekAsync(FreshOrbitDbContext db, string code, string period, CancellationToken cancellationToken)
        => await db.Set<DomainNumberSeries>().AsNoTracking().Where(s => s.Code == code && s.Period == period).Select(s => s.NextValue).SingleAsync(cancellationToken);
}
