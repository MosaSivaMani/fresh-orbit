using FreshOrbit.Locations.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Locations.Domain.Cities;

/// <summary>One missing piece of configuration that blocks activation.</summary>
public sealed record MissingItem(string Code, Guid? FulfillmentLocationId, string Message);

/// <summary>Counts for one ACTIVE fulfillment location of a city.</summary>
/// <param name="ActivePincodes">Active pincodes in the location's active service areas.</param>
public sealed record LocationReadinessCounts(Guid FulfillmentLocationId, int ActiveServiceAreas, int ActivePincodes);

/// <summary>LOC-R6: what a city still needs before it can be activated.</summary>
public static class CityReadiness
{
    /// <summary>
    /// The full missing list: Locations' own items for the city and each ACTIVE location, followed by the items reported
    /// by other modules' checks (<paramref name="reportedByChecks"/>), never just the first.
    /// </summary>
    public static IReadOnlyList<MissingItem> Evaluate(IReadOnlyList<LocationReadinessCounts> activeLocations, IEnumerable<MissingItem> reportedByChecks)
    {
        ArgumentNullException.ThrowIfNull(activeLocations);
        ArgumentNullException.ThrowIfNull(reportedByChecks);
        var missing = new List<MissingItem>();
        if (activeLocations.Count == 0)
        {
            missing.Add(new MissingItem(LocationsErrors.FulfillmentLocationMissing, null, "No active fulfillment location."));
        }

        foreach (var location in activeLocations)
        {
            if (location.ActiveServiceAreas == 0)
            {
                missing.Add(new MissingItem(LocationsErrors.ServiceAreaMissing, location.FulfillmentLocationId, "No active service area."));
            }
            else if (location.ActivePincodes == 0)
            {
                missing.Add(new MissingItem(LocationsErrors.PincodeMissing, location.FulfillmentLocationId, "No active pincode in an active service area."));
            }
        }

        missing.AddRange(reportedByChecks);
        return missing;
    }
}

/// <summary>422 CITY_NOT_READY; <see cref="Missing"/> becomes the <c>missing</c> member of the problem.</summary>
public sealed class CityNotReadyException(string cityName, IReadOnlyList<MissingItem> missing)
    : DomainException(LocationsErrors.CityNotReady, $"{cityName} is missing required configuration.")
{
    public IReadOnlyList<MissingItem> Missing { get; } = missing;
}

/// <summary>422 CITY_RETIREMENT_BLOCKED; <see cref="Blocking"/> becomes the <c>blocking</c> member of the problem.</summary>
public sealed class CityRetirementBlockedException(IReadOnlyList<string> blocking)
    : DomainException(LocationsErrors.CityRetirementBlocked, "The city cannot be retired while work is still open.")
{
    public IReadOnlyList<string> Blocking { get; } = blocking;
}
