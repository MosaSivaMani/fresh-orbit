namespace FreshOrbit.Locations.Domain.FulfillmentLocations;

public interface IFulfillmentLocationRepository
{
    /// <summary>The location with its operating hours and closures.</summary>
    Task<FulfillmentLocation?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);

    /// <summary>ACTIVE locations of the city other than <paramref name="excludingId"/>.</summary>
    Task<int> CountActiveInCityAsync(Guid cityId, Guid excludingId, CancellationToken cancellationToken);

    void Add(FulfillmentLocation location);

    /// <summary>Marks the root row changed so a child-only change still checks and bumps the row version.</summary>
    void MarkChanged(FulfillmentLocation location);
}
