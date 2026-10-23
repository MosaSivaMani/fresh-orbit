using FreshOrbit.Locations.Application.Common;
using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.FulfillmentLocations;
using FreshOrbit.Locations.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Locations.Application.FulfillmentLocations;

/// <summary>Fulfillment location commands: details, status (LOC-R11/R12), hours (LOC-R13) and closures (LOC-R14).</summary>
public sealed class FulfillmentLocationService(
    IFulfillmentLocationRepository locations,
    ICityRepository cities,
    IPincodeResolutionCache cache,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentActor actor)
{
    public async Task<FulfillmentLocationResponse> CreateAsync(CreateFulfillmentLocationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var city = await cities.GetAsync(request.CityId, cancellationToken)
            ?? throw new RefusalException(new FieldError("cityId", LocationsErrors.ValidationFailed, "Choose an existing city."));
        var location = FulfillmentLocation.Create(
            city.Id,
            city.Status == CityStatus.Retired,
            request.Code,
            new LocationDetails(request.Name, request.AddressLine, request.Pincode, request.Phone, request.Latitude, request.Longitude, request.HasColdStorage, request.FssaiLicenseNo));
        if (await locations.CodeExistsAsync(location.Code, cancellationToken))
        {
            throw new ConflictException(LocationsErrors.Duplicate, "Another fulfillment location already uses this code.", "code");
        }

        locations.Add(location);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(location);
    }

    public async Task<FulfillmentLocationResponse> UpdateAsync(Guid locationId, uint expectedVersion, UpdateFulfillmentLocationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var location = await LoadAsync(locationId, expectedVersion, cancellationToken);
        location.Update(new LocationDetails(request.Name, request.AddressLine, request.Pincode, request.Phone, request.Latitude, request.Longitude, request.HasColdStorage, request.FssaiLicenseNo));
        if (request.Status is { } target && target != location.Status)
        {
            var city = await cities.GetAsync(location.CityId, cancellationToken)
                ?? throw new InvalidOperationException($"City {location.CityId} of location {location.Id} does not exist.");
            var otherActive = await locations.CountActiveInCityAsync(city.Id, location.Id, cancellationToken);
            location.ChangeStatus(target, city.Status == CityStatus.Active, city.Status == CityStatus.Retired, otherActive);
        }

        await SaveAsync(location, cancellationToken);
        return ToResponse(location);
    }

    public async Task<FulfillmentLocationResponse> SetOperatingHoursAsync(Guid locationId, uint expectedVersion, SetOperatingHoursRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var days = request.Days ?? [];
        var errors = new List<FieldError>();
        var week = new List<(DayOfWeek, TimeOnly, TimeOnly)>();
        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            if (day is null)
            {
                errors.Add(new FieldError($"days[{i}]", LocationsErrors.ValidationFailed, "Enter the day, opening and closing time."));
                continue;
            }

            var opens = Formats.TryParseTime(day.OpensAt, out var opensAt);
            var closes = Formats.TryParseTime(day.ClosesAt, out var closesAt);
            if (!opens)
            {
                errors.Add(new FieldError($"days[{i}].opensAt", LocationsErrors.ValidationFailed, "Enter the opening time as HH:mm."));
            }

            if (!closes)
            {
                errors.Add(new FieldError($"days[{i}].closesAt", LocationsErrors.ValidationFailed, "Enter the closing time as HH:mm."));
            }

            week.Add((day.DayOfWeek, opensAt, closesAt));
        }

        Rules.ThrowIfAny(errors);
        var location = await LoadAsync(locationId, expectedVersion, cancellationToken);
        location.SetOperatingHours(week);
        await SaveAsync(location, cancellationToken);
        return ToResponse(location);
    }

    public async Task<ClosureResponse> AddClosureAsync(Guid locationId, AddClosureRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var location = await LoadAsync(locationId, null, cancellationToken);
        var closure = location.AddClosure(request.ClosedOn, request.Reason, await TodayAsync(location, cancellationToken));
        await SaveAsync(location, cancellationToken);
        return new ClosureResponse(closure.Id, location.Id, closure.ClosedOn, closure.Reason);
    }

    public async Task RemoveClosureAsync(Guid locationId, Guid closureId, CancellationToken cancellationToken)
    {
        var location = await LoadAsync(locationId, null, cancellationToken);
        location.RemoveClosure(closureId, await TodayAsync(location, cancellationToken));
        await SaveAsync(location, cancellationToken);
    }

    public static FulfillmentLocationResponse ToResponse(FulfillmentLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return new FulfillmentLocationResponse(
            location.Id,
            location.CityId,
            location.Code,
            location.Name,
            location.AddressLine,
            location.Pincode,
            location.Phone,
            location.Latitude,
            location.Longitude,
            location.HasColdStorage,
            location.FssaiLicenseNo,
            location.Status,
            [.. location.OperatingHours.OrderBy(h => h.DayOfWeek).Select(h => new OperatingHoursResponse(h.DayOfWeek, Formats.Time(h.OpensAt), Formats.Time(h.ClosesAt)))],
            Formats.Version(location.Version));
    }

    private async Task<DateOnly> TodayAsync(FulfillmentLocation location, CancellationToken cancellationToken)
    {
        var city = await cities.GetAsync(location.CityId, cancellationToken)
            ?? throw new InvalidOperationException($"City {location.CityId} of location {location.Id} does not exist.");
        return Rules.Today(clock.UtcNow, city.TimeZone);
    }

    private async Task<FulfillmentLocation> LoadAsync(Guid locationId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        LocationAccess.Ensure(actor, locationId);
        var location = await locations.GetAsync(locationId, cancellationToken)
            ?? throw new NotFoundException(LocationsErrors.FulfillmentLocationNotFound, "No fulfillment location has this id.");
        if (expectedVersion is { } version)
        {
            unitOfWork.SetExpectedVersion(location, version);
        }

        return location;
    }

    private async Task SaveAsync(FulfillmentLocation location, CancellationToken cancellationToken)
    {
        locations.MarkChanged(location);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
    }
}
