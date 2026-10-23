using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Locations.Public;

namespace FreshOrbit.Delivery.Infrastructure.Locations;

/// <summary>
/// <see cref="ILocationCalendar"/> over <c>Locations.Public</c> (locations and closures).
/// </summary>
internal sealed class LocationsPublicCalendar(ILocationsApi locations) : ILocationCalendar
{
    public Task<IReadOnlyList<FulfillmentLocationSummary>> GetLocationsAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, CancellationToken cancellationToken)
        => locations.GetFulfillmentLocationsAsync(fulfillmentLocationIds, cancellationToken);

    public async Task<IReadOnlySet<DateOnly>> GetClosedDatesAsync(Guid fulfillmentLocationId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
        => (await locations.GetClosuresAsync(fulfillmentLocationId, fromDate, toDate, cancellationToken)).ToHashSet();
}
