using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Locations.Public;

namespace FreshOrbit.Delivery.Infrastructure.Locations;

/// <summary>
/// <see cref="ILocationCalendar"/> over <c>Locations.Public</c>. Closures: <c>ILocationsApi</c> does not expose them
/// yet (E2 adds <c>GetClosuresAsync(locationId, fromDate, toDate)</c>); until it does, slot generation refuses rather
/// than generating slots on closed dates (CLAUDE.md rule 7).
/// </summary>
internal sealed class LocationsPublicCalendar(ILocationsApi locations) : ILocationCalendar
{
    public Task<IReadOnlyList<FulfillmentLocationSummary>> GetLocationsAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, CancellationToken cancellationToken)
        => locations.GetFulfillmentLocationsAsync(fulfillmentLocationIds, cancellationToken);

    public Task<IReadOnlySet<DateOnly>> GetClosedDatesAsync(Guid fulfillmentLocationId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
        => throw new NotSupportedException("Location closures need ILocationsApi.GetClosuresAsync (Locations.Public); wire it here at merge.");
}
