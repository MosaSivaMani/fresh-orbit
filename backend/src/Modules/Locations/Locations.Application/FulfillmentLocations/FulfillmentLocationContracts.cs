using FreshOrbit.Locations.Domain.FulfillmentLocations;

namespace FreshOrbit.Locations.Application.FulfillmentLocations;

public sealed record CreateFulfillmentLocationRequest(
    Guid CityId,
    string? Code,
    string? Name,
    string? AddressLine,
    string? Pincode,
    string? Phone,
    decimal? Latitude,
    decimal? Longitude,
    bool HasColdStorage,
    string? FssaiLicenseNo);

/// <summary>Replaces the editable fields; <c>Status</c> null keeps the current status.</summary>
public sealed record UpdateFulfillmentLocationRequest(
    string? Name,
    string? AddressLine,
    string? Pincode,
    string? Phone,
    decimal? Latitude,
    decimal? Longitude,
    bool HasColdStorage,
    string? FssaiLicenseNo,
    LocationStatus? Status);

/// <param name="OpensAt">Local time <c>HH:mm</c>.</param>
public sealed record OperatingHoursDay(DayOfWeek DayOfWeek, string? OpensAt, string? ClosesAt);

/// <summary>The whole week (LOC-R13); a day not listed is closed.</summary>
public sealed record SetOperatingHoursRequest(IReadOnlyList<OperatingHoursDay>? Days);

public sealed record AddClosureRequest(DateOnly ClosedOn, string? Reason);

public sealed record OperatingHoursResponse(DayOfWeek DayOfWeek, string OpensAt, string ClosesAt);

public sealed record FulfillmentLocationResponse(
    Guid Id,
    Guid CityId,
    string Code,
    string Name,
    string? AddressLine,
    string? Pincode,
    string? Phone,
    decimal? Latitude,
    decimal? Longitude,
    bool HasColdStorage,
    string? FssaiLicenseNo,
    LocationStatus Status,
    IReadOnlyList<OperatingHoursResponse> OperatingHours,
    string Version);

/// <summary>A location the signed-in staff member may act on (<c>GET /admin/me/locations</c>, any staff).</summary>
public sealed record StaffLocationItem(Guid Id, Guid CityId, string Code, string Name, LocationStatus Status);

public sealed record ClosureResponse(Guid Id, Guid FulfillmentLocationId, DateOnly ClosedOn, string? Reason);
