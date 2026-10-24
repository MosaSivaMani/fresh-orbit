namespace FreshOrbit.Locations.Public;

/// <summary>What other modules may ask the Locations module.</summary>
public interface ILocationsApi
{
    // Members added in Phase 2 have default bodies so test doubles written against the Phase 1 contract still compile;
    // the module's real implementation overrides every one of them.
    private const string NewMemberNote = "Not implemented by this test double.";

    /// <summary>
    /// Where and on what terms a pincode is served (LOC-R1, cached 60 s). A pincode that is not served (unknown,
    /// inactive, or its area / location / city not active) returns <c>IsServiceable = false</c> with nulls.
    /// </summary>
    Task<Serviceability> ResolvePincodeAsync(string pincode, CancellationToken cancellationToken = default);

    /// <summary>The requested locations that exist (missing ids are simply absent).</summary>
    Task<IReadOnlyList<FulfillmentLocationSummary>> GetFulfillmentLocationsAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, CancellationToken cancellationToken = default);

    /// <summary>The locations with the given codes that exist (missing codes are simply absent); codes match exactly.</summary>
    Task<IReadOnlyList<FulfillmentLocationSummary>> GetFulfillmentLocationsByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>Every location of a city, any status.</summary>
    Task<IReadOnlyList<FulfillmentLocationSummary>> GetCityFulfillmentLocationsAsync(Guid cityId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>Dates (in the location's time zone) the location is closed between the two dates, inclusive.</summary>
    Task<IReadOnlyList<DateOnly>> GetClosuresAsync(Guid fulfillmentLocationId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>Weekly opening hours per location; a day without a row is closed.</summary>
    Task<IReadOnlyList<OperatingHoursEntry>> GetOperatingHoursAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>The requested cities that exist, any status (missing ids are simply absent).</summary>
    Task<IReadOnlyList<CitySummary>> GetCitiesAsync(IReadOnlyCollection<Guid> cityIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>The cities with the given codes that exist, any status (missing codes are simply absent); codes match exactly.</summary>
    Task<IReadOnlyList<CitySummary>> GetCitiesByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>
    /// Active pincodes assigned to a service area of any location of the city (any area or location status); empty
    /// for an unknown city. Used to find customers whose saved addresses are in the city.
    /// </summary>
    Task<IReadOnlyList<string>> GetCityPincodesAsync(Guid cityId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);
}

/// <param name="Status">DRAFT, ACTIVE, PAUSED or RETIRED (stored code).</param>
public sealed record CitySummary(Guid Id, string Code, string Name, string StateCode, string Status, string TimeZone);

/// <summary>Stored as DRAFT / ACTIVE / PAUSED.</summary>
public enum FulfillmentLocationStatus
{
    Draft,
    Active,
    Paused,
}

/// <param name="TimeZone">IANA time zone of the city, e.g. <c>Asia/Kolkata</c>.</param>
public sealed record Serviceability(
    bool IsServiceable,
    string Pincode,
    Guid? CityId,
    string? CityCode,
    string? TimeZone,
    Guid? FulfillmentLocationId,
    Guid? ServiceAreaId,
    long? DeliveryFeePaise,
    long? FreeDeliveryAbovePaise,
    long? MinOrderPaise)
{
    /// <summary>Code of the serving location (echoed in <c>X-Fulfillment-Location</c>); null when not serviceable.</summary>
    public string? FulfillmentLocationCode { get; init; }
}

public sealed record FulfillmentLocationSummary(Guid Id, Guid CityId, string Code, string Name, FulfillmentLocationStatus Status, string TimeZone)
{
    public bool HasColdStorage { get; init; }

    public string? FssaiLicenseNo { get; init; }
}

/// <param name="DayOfWeek">Day in the location's time zone.</param>
/// <param name="OpensAt">Local time of day the location opens; <paramref name="ClosesAt"/> is later the same day.</param>
public sealed record OperatingHoursEntry(Guid FulfillmentLocationId, DayOfWeek DayOfWeek, TimeOnly OpensAt, TimeOnly ClosesAt);
