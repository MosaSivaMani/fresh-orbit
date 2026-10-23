using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.Locations.Application.Common;
using FreshOrbit.Locations.Application.Serviceability;
using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.FulfillmentLocations;
using FreshOrbit.Locations.Domain.Shared;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace FreshOrbit.Locations.Infrastructure.Public;

/// <summary>
/// LOC-R16: pincode resolutions are cached in memory for 60 seconds. Every Locations command clears the cache after
/// its save (in this process; other Api instances see the change within 60 seconds).
/// </summary>
public sealed class PincodeResolutionCache(IMemoryCache memoryCache) : IPincodeResolutionCache, IDisposable
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private CancellationTokenSource _reset = new();

    public async Task<ServedPincode?> GetOrResolveAsync(string pincode, Func<Task<ServedPincode?>> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        var key = $"locations:pincode:{pincode}";
        if (memoryCache.TryGetValue(key, out ServedPincode? cached))
        {
            return cached;
        }

        var token = new CancellationChangeToken(Volatile.Read(ref _reset).Token);
        var resolved = await resolve();
        using (var entry = memoryCache.CreateEntry(key))
        {
            entry.Value = resolved;
            entry.AbsoluteExpirationRelativeToNow = Lifetime;
            entry.AddExpirationToken(token);
        }

        return resolved;
    }

    public void Invalidate()
    {
        var previous = Interlocked.Exchange(ref _reset, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
    }

    public void Dispose() => _reset.Dispose();
}

/// <summary>The Locations module's answers to other modules (<see cref="ILocationsApi"/>).</summary>
public sealed class LocationsApi(FreshOrbitDbContext db, ILocationsReads reads, PincodeResolutionCache cache) : ILocationsApi
{
    public async Task<Serviceability> ResolvePincodeAsync(string pincode, CancellationToken cancellationToken = default)
    {
        var served = await ResolveServedAsync(pincode, cancellationToken);
        return served is null
            ? new Serviceability(false, pincode, null, null, null, null, null, null, null, null)
            : new Serviceability(true, served.Pincode, served.CityId, served.CityCode, served.TimeZone, served.FulfillmentLocationId, served.ServiceAreaId, served.DeliveryFeePaise, served.FreeDeliveryAbovePaise, served.MinOrderPaise)
            {
                FulfillmentLocationCode = served.FulfillmentLocationCode,
            };
    }

    /// <summary>LOC-R1 through the 60-second cache; null for a malformed or unserved pincode.</summary>
    public Task<ServedPincode?> ResolveServedAsync(string? pincode, CancellationToken cancellationToken)
        => Rules.IsPincode(pincode)
            ? cache.GetOrResolveAsync(pincode!, () => reads.ResolveAsync(pincode!, cancellationToken))
            : Task.FromResult<ServedPincode?>(null);

    public async Task<IReadOnlyList<FulfillmentLocationSummary>> GetFulfillmentLocationsAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fulfillmentLocationIds);
        var ids = fulfillmentLocationIds.Distinct().ToList();
        return await Summaries(l => ids.Contains(l.Id), cancellationToken);
    }

    public async Task<IReadOnlyList<FulfillmentLocationSummary>> GetFulfillmentLocationsByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codes);
        var list = codes.Distinct(StringComparer.Ordinal).ToList();
        return await Summaries(l => list.Contains(l.Code), cancellationToken);
    }

    public Task<IReadOnlyList<FulfillmentLocationSummary>> GetCityFulfillmentLocationsAsync(Guid cityId, CancellationToken cancellationToken = default)
        => Summaries(l => l.CityId == cityId, cancellationToken);

    public async Task<IReadOnlyList<DateOnly>> GetClosuresAsync(Guid fulfillmentLocationId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
        => await db.Set<LocationClosure>().AsNoTracking()
            .Where(c => c.FulfillmentLocationId == fulfillmentLocationId && c.ClosedOn >= fromDate && c.ClosedOn <= toDate)
            .OrderBy(c => c.ClosedOn)
            .Select(c => c.ClosedOn)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CitySummary>> GetCitiesAsync(IReadOnlyCollection<Guid> cityIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cityIds);
        var ids = cityIds.Distinct().ToList();
        var rows = await db.Set<City>().AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);
        return [.. rows.Select(c => new CitySummary(c.Id, c.Code, c.Name, c.StateCode, c.Status.ToCode(), c.TimeZone))];
    }

    public async Task<IReadOnlyList<OperatingHoursEntry>> GetOperatingHoursAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fulfillmentLocationIds);
        var ids = fulfillmentLocationIds.Distinct().ToList();
        return await db.Set<OperatingHours>().AsNoTracking()
            .Where(h => ids.Contains(h.FulfillmentLocationId))
            .OrderBy(h => h.FulfillmentLocationId)
            .ThenBy(h => h.DayOfWeek)
            .Select(h => new OperatingHoursEntry(h.FulfillmentLocationId, h.DayOfWeek, h.OpensAt, h.ClosesAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<FulfillmentLocationSummary>> Summaries(System.Linq.Expressions.Expression<Func<FulfillmentLocation, bool>> filter, CancellationToken cancellationToken)
    {
        var rows = await db.Set<FulfillmentLocation>().AsNoTracking()
            .Where(filter)
            .Join(db.Set<City>().AsNoTracking(), l => l.CityId, c => c.Id, (l, c) => new { Location = l, c.TimeZone })
            .OrderBy(r => r.Location.Code)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new FulfillmentLocationSummary(r.Location.Id, r.Location.CityId, r.Location.Code, r.Location.Name, ToPublic(r.Location.Status), r.TimeZone)
        {
            HasColdStorage = r.Location.HasColdStorage,
            FssaiLicenseNo = r.Location.FssaiLicenseNo,
        })];
    }

    private static FulfillmentLocationStatus ToPublic(LocationStatus status) => status switch
    {
        LocationStatus.Draft => FulfillmentLocationStatus.Draft,
        LocationStatus.Active => FulfillmentLocationStatus.Active,
        LocationStatus.Paused => FulfillmentLocationStatus.Paused,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
