using System.Collections.Concurrent;
using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Locations.Public;

namespace FreshOrbit.Delivery.Tests.Support;

/// <summary>Hand-written stand-in for Locations: locations, pincodes and closures held in memory.</summary>
public sealed class FakeLocations : ILocationsApi, ILocationCalendar
{
    public const string Ist = "Asia/Kolkata";

    private readonly ConcurrentDictionary<Guid, FulfillmentLocationSummary> _locations = new();
    private readonly ConcurrentDictionary<string, Guid> _pincodes = new();
    private readonly ConcurrentDictionary<(Guid, DateOnly), bool> _closures = new();

    public FulfillmentLocationSummary AddLocation(Guid? cityId = null, FulfillmentLocationStatus status = FulfillmentLocationStatus.Active, string timeZone = Ist)
    {
        var id = Guid.CreateVersion7();
        var location = new FulfillmentLocationSummary(id, cityId ?? Guid.CreateVersion7(), $"LOC-{id:N}"[..12], "Test store", status, timeZone);
        _locations[id] = location;
        return location;
    }

    public void AddPincode(string pincode, Guid locationId) => _pincodes[pincode] = locationId;

    public void AddClosure(Guid locationId, DateOnly date) => _closures[(locationId, date)] = true;

    public void RemoveClosure(Guid locationId, DateOnly date) => _closures.TryRemove((locationId, date), out _);

    public Task<Serviceability> ResolvePincodeAsync(string pincode, CancellationToken cancellationToken = default)
    {
        if (_pincodes.TryGetValue(pincode, out var locationId) && _locations.TryGetValue(locationId, out var location))
        {
            return Task.FromResult(new Serviceability(true, pincode, location.CityId, "TEST", location.TimeZone, location.Id, Guid.CreateVersion7(), 3900, 49900, 29900));
        }

        return Task.FromResult(new Serviceability(false, pincode, null, null, null, null, null, null, null, null));
    }

    public Task<IReadOnlyList<FulfillmentLocationSummary>> GetFulfillmentLocationsAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FulfillmentLocationSummary>>([.. fulfillmentLocationIds.Distinct().Select(id => _locations.GetValueOrDefault(id)).OfType<FulfillmentLocationSummary>()]);

    public Task<IReadOnlyList<FulfillmentLocationSummary>> GetLocationsAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, CancellationToken cancellationToken)
        => GetFulfillmentLocationsAsync(fulfillmentLocationIds, cancellationToken);

    public Task<IReadOnlySet<DateOnly>> GetClosedDatesAsync(Guid fulfillmentLocationId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlySet<DateOnly>>(_closures.Keys
            .Where(k => k.Item1 == fulfillmentLocationId && k.Item2 >= fromDate && k.Item2 <= toDate)
            .Select(k => k.Item2)
            .ToHashSet());
}
