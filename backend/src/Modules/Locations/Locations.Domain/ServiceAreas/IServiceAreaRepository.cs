namespace FreshOrbit.Locations.Domain.ServiceAreas;

public interface IServiceAreaRepository
{
    /// <summary>The service area with all its pincode rows.</summary>
    Task<ServiceArea?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Of the given pincodes, those active in a service area other than <paramref name="serviceAreaId"/> (one query).</summary>
    Task<IReadOnlySet<string>> FindActiveElsewhereAsync(IReadOnlyCollection<string> pincodes, Guid serviceAreaId, CancellationToken cancellationToken);

    void Add(ServiceArea serviceArea);

    /// <summary>Marks the root row changed so a child-only change still checks and bumps the row version.</summary>
    void MarkChanged(ServiceArea serviceArea);
}
