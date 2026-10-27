using FreshOrbit.Locations.Application.Cities;
using FreshOrbit.Locations.Application.Common;
using FreshOrbit.Locations.Application.FulfillmentLocations;
using FreshOrbit.Locations.Application.ServiceAreas;
using FreshOrbit.Locations.Application.Serviceability;
using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.FulfillmentLocations;
using FreshOrbit.Locations.Domain.ServiceAreas;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Locations.Infrastructure.Reads;

public sealed class LocationsReads(FreshOrbitDbContext db) : ILocationsReads
{
    private IQueryable<City> Cities => db.Set<City>().AsNoTracking();

    private IQueryable<FulfillmentLocation> Locations => db.Set<FulfillmentLocation>().AsNoTracking();

    private IQueryable<ServiceArea> Areas => db.Set<ServiceArea>().AsNoTracking();

    private IQueryable<Pincode> Pincodes => db.Set<Pincode>().AsNoTracking();

    public async Task<(IReadOnlyList<CityResponse> Items, int TotalCount)> ListCitiesAsync(IReadOnlyCollection<CityStatus> statuses, int skip, int take, CancellationToken cancellationToken)
    {
        var query = Cities;
        if (statuses.Count > 0)
        {
            var list = statuses.ToList();
            query = query.Where(c => list.Contains(c.Status));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(c => c.Name).ThenBy(c => c.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return ([.. items.Select(CityService.ToResponse)], total);
    }

    public async Task<IReadOnlyList<PublicCityResponse>> ListActiveCitiesAsync(CancellationToken cancellationToken)
        => await Cities.Where(c => c.Status == CityStatus.Active)
            .OrderBy(c => c.Name)
            .Select(c => new PublicCityResponse(c.Id, c.Code, c.Name, c.StateCode) { StateName = c.StateName })
            .ToListAsync(cancellationToken);

    public async Task<CityResponse?> GetCityAsync(Guid cityId, CancellationToken cancellationToken)
    {
        var city = await Cities.FirstOrDefaultAsync(c => c.Id == cityId, cancellationToken);
        return city is null ? null : CityService.ToResponse(city);
    }

    public async Task<IReadOnlyList<LocationReadinessCounts>> GetReadinessCountsAsync(Guid cityId, CancellationToken cancellationToken)
        => await Locations
            .Where(l => l.CityId == cityId && l.Status == LocationStatus.Active)
            .Select(l => new LocationReadinessCounts(
                l.Id,
                Areas.Count(a => a.FulfillmentLocationId == l.Id && a.IsActive),
                Pincodes.Count(p => p.IsActive && Areas.Any(a => a.Id == p.ServiceAreaId && a.FulfillmentLocationId == l.Id && a.IsActive))))
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<FulfillmentLocationResponse> Items, int TotalCount)> ListLocationsAsync(Guid? cityId, IReadOnlySet<Guid>? visibleIds, int skip, int take, CancellationToken cancellationToken)
    {
        var query = Locations.Include(l => l.OperatingHours).AsQueryable();
        if (cityId is { } city)
        {
            query = query.Where(l => l.CityId == city);
        }

        if (visibleIds is not null)
        {
            var ids = visibleIds.ToList();
            query = query.Where(l => ids.Contains(l.Id));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(l => l.Code).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return ([.. items.Select(FulfillmentLocationService.ToResponse)], total);
    }

    public async Task<(IReadOnlyList<CityLookupItem> Items, int TotalCount)> LookupCitiesAsync(IReadOnlyCollection<CityStatus> statuses, int skip, int take, CancellationToken cancellationToken)
    {
        var query = Cities;
        if (statuses.Count > 0)
        {
            var list = statuses.ToList();
            query = query.Where(c => list.Contains(c.Status));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(c => c.Name).ThenBy(c => c.Id).Skip(skip).Take(take)
            .Select(c => new CityLookupItem(c.Id, c.Code, c.Name, c.Status))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(IReadOnlyList<StaffLocationItem> Items, int TotalCount)> ListStaffLocationsAsync(IReadOnlySet<Guid>? visibleIds, int skip, int take, CancellationToken cancellationToken)
    {
        var query = Locations;
        if (visibleIds is not null)
        {
            var ids = visibleIds.ToList();
            query = query.Where(l => ids.Contains(l.Id));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(l => l.Code).ThenBy(l => l.Id).Skip(skip).Take(take)
            .Select(l => new StaffLocationItem(l.Id, l.CityId, l.Code, l.Name, l.Status))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<FulfillmentLocationResponse?> GetLocationAsync(Guid locationId, CancellationToken cancellationToken)
    {
        var location = await Locations.Include(l => l.OperatingHours).FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken);
        return location is null ? null : FulfillmentLocationService.ToResponse(location);
    }

    public async Task<IReadOnlyList<ClosureResponse>> ListClosuresAsync(Guid locationId, DateOnly? fromDate, CancellationToken cancellationToken)
    {
        var query = db.Set<LocationClosure>().AsNoTracking().Where(c => c.FulfillmentLocationId == locationId);
        if (fromDate is { } from)
        {
            query = query.Where(c => c.ClosedOn >= from);
        }

        return await query.OrderBy(c => c.ClosedOn)
            .Select(c => new ClosureResponse(c.Id, c.FulfillmentLocationId, c.ClosedOn, c.Reason))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ServiceAreaResponse> Items, int TotalCount)> ListServiceAreasAsync(Guid? cityId, Guid? fulfillmentLocationId, IReadOnlySet<Guid>? visibleLocationIds, int skip, int take, CancellationToken cancellationToken)
    {
        var query = Areas;
        if (cityId is { } city)
        {
            query = query.Where(a => a.CityId == city);
        }

        if (fulfillmentLocationId is { } location)
        {
            query = query.Where(a => a.FulfillmentLocationId == location);
        }

        if (visibleLocationIds is not null)
        {
            var ids = visibleLocationIds.ToList();
            query = query.Where(a => ids.Contains(a.FulfillmentLocationId));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await Project(query.OrderBy(a => a.Name).ThenBy(a => a.Id).Skip(skip).Take(take)).ToListAsync(cancellationToken);
        return ([.. rows.Select(r => r.ToResponse())], total);
    }

    public async Task<ServiceAreaResponse?> GetServiceAreaAsync(Guid serviceAreaId, CancellationToken cancellationToken)
        => (await Project(Areas.Where(a => a.Id == serviceAreaId)).FirstOrDefaultAsync(cancellationToken))?.ToResponse();

    public async Task<IReadOnlyList<PincodeResponse>> ListPincodesAsync(Guid serviceAreaId, bool includeRemoved, CancellationToken cancellationToken)
        => await Pincodes
            .Where(p => p.ServiceAreaId == serviceAreaId && (includeRemoved || p.IsActive))
            .OrderBy(p => p.Code)
            .ThenByDescending(p => p.IsActive)
            .Select(p => new PincodeResponse(p.Code, p.IsActive, EF.Property<DateTimeOffset>(p, AuditColumns.CreatedAt)))
            .ToListAsync(cancellationToken);

    public Task<ServedPincode?> ResolveAsync(string pincode, CancellationToken cancellationToken)
        => (from p in Pincodes
            where p.Code == pincode && p.IsActive
            join a in Areas on p.ServiceAreaId equals a.Id
            where a.IsActive
            join l in Locations on a.FulfillmentLocationId equals l.Id
            where l.Status == LocationStatus.Active
            join c in Cities on l.CityId equals c.Id
            where c.Status == CityStatus.Active
            select new ServedPincode(
                p.Code,
                c.Id,
                c.Code,
                c.Name,
                c.TimeZone,
                c.StateCode,
                c.StateName,
                l.Id,
                l.Code,
                l.Name,
                l.HasColdStorage,
                a.Id,
                a.Name,
                a.DeliveryFeePaise,
                a.FreeDeliveryAbovePaise,
                a.MinOrderPaise))
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<ServiceAreaRow> Project(IQueryable<ServiceArea> query)
        => query.Select(a => new ServiceAreaRow(a, Pincodes.Count(p => p.ServiceAreaId == a.Id && p.IsActive)));

    private sealed record ServiceAreaRow(ServiceArea Area, int ActivePincodes)
    {
        public ServiceAreaResponse ToResponse() => ServiceAreaService.ToResponse(Area) with { ActivePincodeCount = ActivePincodes };
    }
}
