using FreshOrbit.Locations.Application.Cities;
using FreshOrbit.Locations.Application.FulfillmentLocations;
using FreshOrbit.Locations.Application.ServiceAreas;
using FreshOrbit.Locations.Application.Serviceability;
using FreshOrbit.Locations.Domain.Cities;

namespace FreshOrbit.Locations.Application.Common;

/// <summary>Read-only queries of the Locations module (no tracking, one statement each).</summary>
public interface ILocationsReads
{
    Task<(IReadOnlyList<CityResponse> Items, int TotalCount)> ListCitiesAsync(IReadOnlyCollection<CityStatus> statuses, int skip, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<PublicCityResponse>> ListActiveCitiesAsync(CancellationToken cancellationToken);

    Task<CityResponse?> GetCityAsync(Guid cityId, CancellationToken cancellationToken);

    /// <summary>For each ACTIVE location of the city: active service areas and active pincodes in them (LOC-R6).</summary>
    Task<IReadOnlyList<LocationReadinessCounts>> GetReadinessCountsAsync(Guid cityId, CancellationToken cancellationToken);

    /// <param name="visibleIds">null = all locations; otherwise only these (staff location scope).</param>
    Task<(IReadOnlyList<FulfillmentLocationResponse> Items, int TotalCount)> ListLocationsAsync(Guid? cityId, IReadOnlySet<Guid>? visibleIds, int skip, int take, CancellationToken cancellationToken);

    Task<FulfillmentLocationResponse?> GetLocationAsync(Guid locationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClosureResponse>> ListClosuresAsync(Guid locationId, DateOnly? fromDate, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ServiceAreaResponse> Items, int TotalCount)> ListServiceAreasAsync(Guid? cityId, Guid? fulfillmentLocationId, IReadOnlySet<Guid>? visibleLocationIds, int skip, int take, CancellationToken cancellationToken);

    Task<ServiceAreaResponse?> GetServiceAreaAsync(Guid serviceAreaId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PincodeResponse>> ListPincodesAsync(Guid serviceAreaId, bool includeRemoved, CancellationToken cancellationToken);

    /// <summary>LOC-R1 in one statement; null when the pincode is not served.</summary>
    Task<ServedPincode?> ResolveAsync(string pincode, CancellationToken cancellationToken);
}
