using FreshOrbit.Locations.Application.Cities;
using FreshOrbit.Locations.Application.FulfillmentLocations;
using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.FulfillmentLocations;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Locations.Infrastructure.Seeding;

/// <summary>
/// Order 90: activates the sample locations, then the cities (<c>targetStatus: ACTIVE</c>) through the normal
/// commands, so the LOC-R6 readiness check proves the set is complete. Already active rows are left alone.
/// </summary>
public sealed partial class LocationsActivationSampleDataLoader(
    CityService cities,
    FulfillmentLocationService locations,
    FreshOrbitDbContext db,
    ILogger<LocationsActivationSampleDataLoader> logger) : ISampleDataLoader
{
    public int Order => 90;

    public async Task LoadAsync(SampleDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var file = await SampleFiles.ReadAsync<LocationsSampleDataLoader.LocationsFile>(context, LocationsSampleDataLoader.FileName, cancellationToken);
        var locationCodes = file.FulfillmentLocations.Where(l => l.TargetStatus == "ACTIVE").Select(l => l.Code).ToList();
        var activated = 0;
        foreach (var location in await db.Set<FulfillmentLocation>().AsNoTracking().Where(l => locationCodes.Contains(l.Code) && l.Status != LocationStatus.Active).ToListAsync(cancellationToken))
        {
            await locations.UpdateAsync(
                location.Id,
                location.Version,
                new UpdateFulfillmentLocationRequest(location.Name, location.AddressLine, location.Pincode, location.Phone, location.Latitude, location.Longitude, location.HasColdStorage, location.FssaiLicenseNo, LocationStatus.Active),
                cancellationToken);
            activated++;
        }

        var cityCodes = file.Cities.Where(c => c.TargetStatus == "ACTIVE").Select(c => c.Code).ToList();
        foreach (var city in await db.Set<City>().AsNoTracking().Where(c => cityCodes.Contains(c.Code) && c.Status != CityStatus.Active).ToListAsync(cancellationToken))
        {
            try
            {
                await cities.ActivateAsync(city.Id, city.Version, cancellationToken);
            }
            catch (CityNotReadyException exception)
            {
                throw new InvalidOperationException($"Sample city {city.Code} is not ready: {string.Join("; ", exception.Missing.Select(m => $"{m.Code} {m.Message}"))}", exception);
            }

            activated++;
        }

        LogActivated(activated);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample locations and cities activated: {Count}")]
    private partial void LogActivated(int count);
}
