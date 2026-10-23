using FreshOrbit.Cart.Public;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Ordering.Tests.Integration;

/// <summary>The API host with the other modules' Public APIs replaced by <see cref="CheckoutWorld"/>.</summary>
public sealed class OrderingApiFactory(OrderingDatabase database, CheckoutWorld world) : ApiFactory(database)
{
    public CheckoutWorld World => world;

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
        services.Replace(ServiceDescriptor.Scoped<ISettingsApi, DbNumberSeriesSettings>());
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
/// Settings as seen by Ordering: policy and business profile from the world; number series as a row-locked counter
/// incremented in the caller's transaction (rolled back with it, so numbers stay gapless).
/// </summary>
public sealed class DbNumberSeriesSettings(CheckoutWorld world, FreshOrbitDbContext db) : ISettingsApi
{
    public Task<OrderPolicy> GetOrderPolicyAsync(Guid? cityId, CancellationToken cancellationToken = default) => Task.FromResult(world.Policy);

    public Task<BusinessProfile> GetBusinessProfileAsync(CancellationToken cancellationToken = default) => Task.FromResult(world.Profile);

    public async Task<string> NextNumberAsync(string seriesCode, string period, CancellationToken cancellationToken = default)
    {
        var value = await NumberSeries.NextAsync(db, seriesCode, period, cancellationToken);
        return seriesCode == "INVOICE" ? $"INV/{period}/{value:000000}" : $"FO-{period}{value:0000}";
    }
}

public static class NumberSeries
{
    public static async Task<int> NextAsync(FreshOrbitDbContext db, string code, string period, CancellationToken cancellationToken)
    {
        var values = await db.Database.SqlQuery<int>($"""
            INSERT INTO ordering_test.number_series (code, period, next_value) VALUES ({code}, {period}, 2)
            ON CONFLICT (code, period) DO UPDATE SET next_value = ordering_test.number_series.next_value + 1
            RETURNING next_value - 1 AS "Value"
            """).ToListAsync(cancellationToken);
        return values[0];
    }

    public static async Task<int> PeekAsync(FreshOrbitDbContext db, string code, string period, CancellationToken cancellationToken)
    {
        var values = await db.Database.SqlQuery<int>($"""SELECT next_value AS "Value" FROM ordering_test.number_series WHERE code = {code} AND period = {period}""").ToListAsync(cancellationToken);
        return values.Count == 0 ? 1 : values[0];
    }
}
