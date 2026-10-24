using FreshOrbit.Locations.Domain.Cities;

namespace FreshOrbit.Locations.Application.Cities;

public sealed record CreateCityRequest(string? Code, string? Name, string? StateCode, string? TimeZone);

/// <summary>The code is immutable (LOC-R10).</summary>
public sealed record UpdateCityRequest(string? Name, string? StateCode, string? TimeZone);

public sealed record CityResponse(
    Guid Id,
    string Code,
    string Name,
    string StateCode,
    string TimeZone,
    CityStatus Status,
    DateTimeOffset? ActivatedAt,
    Guid? ActivatedBy,
    string Version);

public sealed record MissingItemResponse(string Code, Guid? FulfillmentLocationId, string Message);

public sealed record ReadinessResponse(Guid CityId, bool IsReady, IReadOnlyList<MissingItemResponse> Missing);

public sealed record CityDetailResponse(
    Guid Id,
    string Code,
    string Name,
    string StateCode,
    string TimeZone,
    CityStatus Status,
    DateTimeOffset? ActivatedAt,
    Guid? ActivatedBy,
    string Version,
    ReadinessResponse Readiness);

/// <summary>An active city for the customer city picker.</summary>
public sealed record PublicCityResponse(Guid Id, string Code, string Name, string StateCode);

/// <summary>A city in the staff lookup (<c>GET /admin/cities/lookup</c>, any staff): names for pickers and labels.</summary>
public sealed record CityLookupItem(Guid Id, string Code, string Name, CityStatus Status);
