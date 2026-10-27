using FreshOrbit.Locations.Application.Common;
using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.Shared;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Locations.Application.Cities;

/// <summary>City commands (Read → Decide → Commit → Follow-on) and the readiness check (LOC-R6, LOC-R7).</summary>
public sealed class CityService(
    ICityRepository cities,
    ILocationsReads reads,
    IEnumerable<ICityActivationCheck> activationChecks,
    IEnumerable<ICityRetirementCheck> retirementChecks,
    IPincodeResolutionCache cache,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentActor actor)
{
    public async Task<CityResponse> CreateAsync(CreateCityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var city = City.Create(request.Code, request.Name, request.StateCode, request.TimeZone, request.StateName);
        if (await cities.CodeExistsAsync(city.Code, cancellationToken))
        {
            throw new ConflictException(LocationsErrors.Duplicate, "Another city already uses this code.", "code");
        }

        cities.Add(city);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(city);
    }

    public async Task<CityResponse> UpdateAsync(Guid cityId, uint expectedVersion, UpdateCityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var city = await LoadAsync(cityId, expectedVersion, cancellationToken);
        city.Update(request.Name, request.StateCode, request.TimeZone, request.StateName);
        await SaveAsync(cancellationToken);
        return ToResponse(city);
    }

    public async Task<ReadinessResponse> GetReadinessAsync(Guid cityId, CancellationToken cancellationToken)
    {
        _ = await reads.GetCityAsync(cityId, cancellationToken) ?? throw CityNotFound();
        var missing = await EvaluateAsync(cityId, cancellationToken);
        return ToReadiness(cityId, missing);
    }

    public async Task<CityResponse> ActivateAsync(Guid cityId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var city = await LoadAsync(cityId, expectedVersion, cancellationToken);
        var missing = await EvaluateAsync(cityId, cancellationToken);
        city.Activate(missing, clock.UtcNow, actor.UserId);
        await SaveAsync(cancellationToken);
        return ToResponse(city);
    }

    public async Task<CityResponse> PauseAsync(Guid cityId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var city = await LoadAsync(cityId, expectedVersion, cancellationToken);
        city.Pause();
        await SaveAsync(cancellationToken);
        return ToResponse(city);
    }

    public async Task<CityResponse> RetireAsync(Guid cityId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var city = await LoadAsync(cityId, expectedVersion, cancellationToken);
        var blocking = new List<string>();
        foreach (var check in retirementChecks)
        {
            blocking.AddRange(await check.GetBlockingItemsAsync(cityId, cancellationToken));
        }

        city.Retire(blocking);
        await SaveAsync(cancellationToken);
        return ToResponse(city);
    }

    public static CityResponse ToResponse(City city)
    {
        ArgumentNullException.ThrowIfNull(city);
        return new CityResponse(city.Id, city.Code, city.Name, city.StateCode, city.TimeZone, city.Status, city.ActivatedAt, city.ActivatedBy, Formats.Version(city.Version))
        {
            StateName = city.StateName,
        };
    }

    public static ReadinessResponse ToReadiness(Guid cityId, IReadOnlyList<MissingItem> missing)
    {
        ArgumentNullException.ThrowIfNull(missing);
        return new ReadinessResponse(cityId, missing.Count == 0, [.. missing.Select(m => new MissingItemResponse(m.Code, m.FulfillmentLocationId, m.Message))]);
    }

    /// <summary>LOC-R6: own counts in one query, then each registered check once with all ACTIVE location ids.</summary>
    private async Task<IReadOnlyList<MissingItem>> EvaluateAsync(Guid cityId, CancellationToken cancellationToken)
    {
        var counts = await reads.GetReadinessCountsAsync(cityId, cancellationToken);
        var activeIds = counts.Select(c => c.FulfillmentLocationId).ToList();
        var reported = new List<MissingItem>();
        foreach (var check in activationChecks)
        {
            var items = await check.GetMissingDetailsAsync(cityId, activeIds, cancellationToken);
            reported.AddRange(items.Select(i => new MissingItem(i.Code, i.FulfillmentLocationId, i.Message)));
        }

        return CityReadiness.Evaluate(counts, reported);
    }

    private async Task<City> LoadAsync(Guid cityId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var city = await cities.GetAsync(cityId, cancellationToken) ?? throw CityNotFound();
        unitOfWork.SetExpectedVersion(city, expectedVersion);
        return city;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
    }

    private static NotFoundException CityNotFound() => new(LocationsErrors.CityNotFound, "No city has this id.");
}
