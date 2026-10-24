namespace FreshOrbit.Locations.Application.Serviceability;

/// <summary>A served pincode with everything the serviceability answer shows (LOC-R1).</summary>
public sealed record ServedPincode(
    string Pincode,
    Guid CityId,
    string CityCode,
    string CityName,
    string TimeZone,
    Guid FulfillmentLocationId,
    string FulfillmentLocationCode,
    string FulfillmentLocationName,
    bool HasColdStorage,
    Guid ServiceAreaId,
    string ServiceAreaName,
    long DeliveryFeePaise,
    long? FreeDeliveryAbovePaise,
    long MinOrderPaise);

/// <param name="Name">City name for display ("Top picks in {name}").</param>
/// <param name="TimeZone">IANA time zone of the city (W10); required on every city, so never null here.</param>
public sealed record ServiceabilityCity(Guid Id, string Code, string Name, string TimeZone);

public sealed record ServiceabilityLocation(Guid Id, string Code, string Name, bool HasColdStorage);

public sealed record ServiceabilityArea(Guid Id, string Name, long DeliveryFeePaise, long? FreeDeliveryAbovePaise, long MinOrderPaise);

/// <summary>
/// The next deliverable slot (Delivery's rules: OPEN, cutoff not passed, capacity left); null when none. Times are UTC;
/// <c>Date</c> is the slot's local date in the city's time zone.
/// </summary>
public sealed record ServiceabilitySlot(Guid Id, DateTimeOffset StartAt, DateTimeOffset EndAt)
{
    public DateOnly Date { get; init; }

    public DateTimeOffset CutoffAt { get; init; }
}

/// <summary><c>GET /serviceability</c>: always 200; <c>Serviceable = false</c> with <c>ReasonCode</c> and nulls when not served.</summary>
public sealed record ServiceabilityResponse(
    string Pincode,
    bool Serviceable,
    string? ReasonCode,
    ServiceabilityCity? City,
    ServiceabilityLocation? FulfillmentLocation,
    ServiceabilityArea? ServiceArea,
    ServiceabilitySlot? NextSlot);
