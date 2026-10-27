using System.Globalization;
using FreshOrbit.Locations.Application.Cities;
using FreshOrbit.Locations.Application.FulfillmentLocations;
using FreshOrbit.Locations.Application.ServiceAreas;
using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.FulfillmentLocations;
using FreshOrbit.Locations.Domain.ServiceAreas;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Locations.Infrastructure.Seeding;

/// <summary>
/// Sample <c>locations.json</c> (order 10): cities, fulfillment locations with their week, closures, service areas
/// and pincodes, all through the Locations services. City and location stay DRAFT here; they are activated by
/// <see cref="LocationsActivationSampleDataLoader"/> once every module has loaded (LOC-R6).
/// </summary>
public sealed partial class LocationsSampleDataLoader(
    CityService cities,
    FulfillmentLocationService locations,
    ServiceAreaService areas,
    FreshOrbitDbContext db,
    ILogger<LocationsSampleDataLoader> logger) : ISampleDataLoader
{
    public const string FileName = "locations.json";

    public int Order => 10;

    public async Task LoadAsync(SampleDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var file = await SampleFiles.ReadAsync<LocationsFile>(context, FileName, cancellationToken);

        // Cities
        var cityCodes = file.Cities.Select(c => c.Code).ToList();
        var cityIds = await db.Set<City>().AsNoTracking().Where(c => cityCodes.Contains(c.Code)).ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);
        var cityZones = await db.Set<City>().AsNoTracking().Where(c => cityCodes.Contains(c.Code)).ToDictionaryAsync(c => c.Code, c => c.TimeZone, cancellationToken);
        var insertedCities = 0;
        foreach (var row in file.Cities.Where(c => !cityIds.ContainsKey(c.Code)))
        {
            var city = await cities.CreateAsync(new CreateCityRequest(row.Code, row.Name, row.StateCode, row.TimeZone, row.StateName), cancellationToken);
            context.Track<City>(db, city.Id);
            cityIds[row.Code] = city.Id;
            cityZones[row.Code] = city.TimeZone;
            insertedCities++;
        }

        // Fulfillment locations and their operating hours
        var locationCodes = file.FulfillmentLocations.Select(l => l.Code).ToList();
        var locationIds = await db.Set<FulfillmentLocation>().AsNoTracking().Where(l => locationCodes.Contains(l.Code)).ToDictionaryAsync(l => l.Code, l => l.Id, cancellationToken);
        var locationZones = new Dictionary<string, string>(StringComparer.Ordinal);
        var insertedLocations = 0;
        foreach (var row in file.FulfillmentLocations)
        {
            var cityId = cityIds.GetValueOrDefault(row.CityCode);
            if (cityId == Guid.Empty)
            {
                throw SampleFiles.Missing(FileName, "city", row.CityCode);
            }

            locationZones[row.Code] = cityZones[row.CityCode];
            if (locationIds.ContainsKey(row.Code))
            {
                continue;
            }

            var created = await locations.CreateAsync(
                new CreateFulfillmentLocationRequest(cityId, row.Code, row.Name, row.AddressLine, row.Pincode, row.Phone, row.Latitude, row.Longitude, row.HasColdStorage, row.FssaiLicenseNo),
                cancellationToken);
            context.Track<FulfillmentLocation>(db, created.Id);
            locationIds[row.Code] = created.Id;
            insertedLocations++;
            var week = file.OperatingHours.FirstOrDefault(h => h.LocationCode == row.Code);
            if (week is not null)
            {
                await locations.SetOperatingHoursAsync(
                    created.Id,
                    uint.Parse(created.Version, CultureInfo.InvariantCulture),
                    new SetOperatingHoursRequest([.. week.Days.Select(d => new OperatingHoursDay(d.DayOfWeek, d.OpensAt, d.ClosesAt))]),
                    cancellationToken);
            }
        }

        // Closures (dates relative to the city-local date of T0)
        var closureLocationIds = locationIds.Values.ToList();
        var existingClosures = (await db.Set<LocationClosure>().AsNoTracking().Where(c => closureLocationIds.Contains(c.FulfillmentLocationId)).Select(c => new { c.FulfillmentLocationId, c.ClosedOn }).ToListAsync(cancellationToken))
            .Select(c => (c.FulfillmentLocationId, c.ClosedOn)).ToHashSet();
        var insertedClosures = 0;
        foreach (var row in file.Closures)
        {
            var locationId = LocationId(locationIds, row.LocationCode);
            var closedOn = SampleFiles.LocalDate(context.StartedAt, locationZones[row.LocationCode], row.ClosedOnOffsetDays);
            if (existingClosures.Contains((locationId, closedOn)))
            {
                continue;
            }

            var closure = await locations.AddClosureAsync(locationId, new AddClosureRequest(closedOn, row.Reason), cancellationToken);
            context.Track<LocationClosure>(db, closure.Id);
            insertedClosures++;
        }

        // Service areas (matched by name within their location) and their pincodes
        var areaRows = await db.Set<ServiceArea>().AsNoTracking().Where(a => closureLocationIds.Contains(a.FulfillmentLocationId)).Select(a => new { a.FulfillmentLocationId, a.Name }).ToListAsync(cancellationToken);
        var existingAreas = areaRows.Select(a => (a.FulfillmentLocationId, a.Name)).ToHashSet();
        var insertedAreas = 0;
        var insertedPincodes = 0;
        foreach (var row in file.ServiceAreas)
        {
            var locationId = LocationId(locationIds, row.LocationCode);
            if (existingAreas.Contains((locationId, row.Name)))
            {
                continue;
            }

            var area = await areas.CreateAsync(
                new CreateServiceAreaRequest(cityIds.GetValueOrDefault(row.CityCode), locationId, row.Name, row.DeliveryFeePaise, row.FreeDeliveryAbovePaise, row.MinOrderPaise),
                cancellationToken);
            context.Track<ServiceArea>(db, area.Id);
            insertedAreas++;
            if (row.Pincodes.Count > 0)
            {
                var added = await areas.AddPincodesAsync(area.Id, uint.Parse(area.Version, CultureInfo.InvariantCulture), new AddPincodesRequest([.. row.Pincodes]), cancellationToken);
                insertedPincodes += added.Added.Count;
            }

            var pincodeIds = await db.Set<Pincode>().AsNoTracking().Where(p => p.ServiceAreaId == area.Id).Select(p => p.Id).ToListAsync(cancellationToken);
            context.Track<Pincode>(db, pincodeIds);
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded(insertedCities, insertedLocations, insertedClosures, insertedAreas, insertedPincodes);
    }

    private static Guid LocationId(Dictionary<string, Guid> locationIds, string code)
        => locationIds.TryGetValue(code, out var id) ? id : throw SampleFiles.Missing(FileName, "fulfillment location", code);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample locations inserted: {Cities} cities, {Locations} locations, {Closures} closures, {Areas} service areas, {Pincodes} pincodes (existing keys skipped)")]
    private partial void LogLoaded(int cities, int locations, int closures, int areas, int pincodes);

    internal sealed record LocationsFile(
        IReadOnlyList<CityRow> Cities,
        IReadOnlyList<LocationRow> FulfillmentLocations,
        IReadOnlyList<WeekRow> OperatingHours,
        IReadOnlyList<ClosureRow> Closures,
        IReadOnlyList<AreaRow> ServiceAreas);

    internal sealed record CityRow(string Code, string Name, string StateCode, string TimeZone, string? TargetStatus, string? StateName = null);

    internal sealed record LocationRow(
        string Code,
        string CityCode,
        string Name,
        string? AddressLine,
        string? Pincode,
        string? Phone,
        decimal? Latitude,
        decimal? Longitude,
        bool HasColdStorage,
        string? FssaiLicenseNo,
        string? TargetStatus);

    internal sealed record WeekRow(string LocationCode, IReadOnlyList<DayRow> Days);

    internal sealed record DayRow(DayOfWeek DayOfWeek, string OpensAt, string ClosesAt);

    internal sealed record ClosureRow(string LocationCode, int ClosedOnOffsetDays, string? Reason);

    internal sealed record AreaRow(
        string Key,
        string CityCode,
        string LocationCode,
        string Name,
        long DeliveryFeePaise,
        long? FreeDeliveryAbovePaise,
        long MinOrderPaise,
        IReadOnlyList<string> Pincodes);
}
