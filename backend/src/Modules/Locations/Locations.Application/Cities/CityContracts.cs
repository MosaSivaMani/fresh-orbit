using FreshOrbit.Locations.Domain.Cities;

namespace FreshOrbit.Locations.Application.Cities;

/// <param name="StateName">Optional display name of the state ("Andhra Pradesh"), shown by <c>GET /serviceability</c>.</param>
public sealed record CreateCityRequest(string? Code, string? Name, string? StateCode, string? TimeZone, string? StateName = null);

/// <summary>The code is immutable (LOC-R10). An omitted or blank <paramref name="StateName"/> clears it.</summary>
public sealed record UpdateCityRequest(string? Name, string? StateCode, string? TimeZone, string? StateName = null);

public sealed record CityResponse(
    Guid Id,
    string Code,
    string Name,
    string StateCode,
    string TimeZone,
    CityStatus Status,
    DateTimeOffset? ActivatedAt,
    Guid? ActivatedBy,
    string Version)
{
    /// <summary>Display name of the state; null when not configured.</summary>
    public string? StateName { get; init; }
}

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
    ReadinessResponse Readiness)
{
    /// <summary>Display name of the state; null when not configured.</summary>
    public string? StateName { get; init; }
}

/// <summary>An active city for the customer city picker.</summary>
public sealed record PublicCityResponse(Guid Id, string Code, string Name, string StateCode)
{
    /// <summary>Display name of the state; null when not configured.</summary>
    public string? StateName { get; init; }
}

/// <summary>A city in the staff lookup (<c>GET /admin/cities/lookup</c>, any staff): names for pickers and labels.</summary>
public sealed record CityLookupItem(Guid Id, string Code, string Name, CityStatus Status);
