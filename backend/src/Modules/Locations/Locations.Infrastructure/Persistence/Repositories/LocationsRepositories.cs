using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.FulfillmentLocations;
using FreshOrbit.Locations.Domain.ServiceAreas;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Locations.Infrastructure.Persistence.Repositories;

public sealed class CityRepository(FreshOrbitDbContext db) : ICityRepository
{
    public Task<City?> GetAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<City>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken)
        => db.Set<City>().AnyAsync(c => c.Code == code, cancellationToken);

    public void Add(City city) => db.Add(city);
}

public sealed class FulfillmentLocationRepository(FreshOrbitDbContext db) : IFulfillmentLocationRepository
{
    public Task<FulfillmentLocation?> GetAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<FulfillmentLocation>()
            .Include(l => l.OperatingHours)
            .Include(l => l.Closures)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken)
        => db.Set<FulfillmentLocation>().AnyAsync(l => l.Code == code, cancellationToken);

    public Task<int> CountActiveInCityAsync(Guid cityId, Guid excludingId, CancellationToken cancellationToken)
        => db.Set<FulfillmentLocation>().CountAsync(l => l.CityId == cityId && l.Id != excludingId && l.Status == LocationStatus.Active, cancellationToken);

    public void Add(FulfillmentLocation location) => db.Add(location);

    public void MarkChanged(FulfillmentLocation location) => db.MarkRootChanged(location);
}

public sealed class ServiceAreaRepository(FreshOrbitDbContext db) : IServiceAreaRepository
{
    public Task<ServiceArea?> GetAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<ServiceArea>().Include(a => a.Pincodes).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlySet<string>> FindActiveElsewhereAsync(IReadOnlyCollection<string> pincodes, Guid serviceAreaId, CancellationToken cancellationToken)
    {
        var codes = pincodes.ToList();
        var found = await db.Set<Pincode>()
            .Where(p => p.IsActive && p.ServiceAreaId != serviceAreaId && codes.Contains(p.Code))
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);
        return found.ToHashSet(StringComparer.Ordinal);
    }

    public void Add(ServiceArea serviceArea) => db.Add(serviceArea);

    public void MarkChanged(ServiceArea serviceArea) => db.MarkRootChanged(serviceArea);
}

internal static class DbContextExtensions
{
    /// <summary>
    /// Makes the save update the aggregate root row even when only children changed, so the If-Match version is
    /// checked against <c>xmin</c> and a new version is issued.
    /// </summary>
    public static void MarkRootChanged(this FreshOrbitDbContext db, object aggregate)
    {
        var entry = db.Entry(aggregate);
        if (entry.State == EntityState.Unchanged)
        {
            entry.Property(AuditColumns.ModifiedAt).IsModified = true;
        }
    }
}
