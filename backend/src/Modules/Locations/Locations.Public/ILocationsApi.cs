namespace FreshOrbit.Locations.Public;

/// <summary>What other modules may ask the Locations module.</summary>
public interface ILocationsApi
{
    /// <summary>Where and on what terms a pincode is served. A pincode that is not served returns <c>IsServiceable = false</c> with nulls.</summary>
    Task<Serviceability> ResolvePincodeAsync(string pincode, CancellationToken cancellationToken = default);

    /// <summary>The requested locations that exist (missing ids are simply absent).</summary>
    Task<IReadOnlyList<FulfillmentLocationSummary>> GetFulfillmentLocationsAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, CancellationToken cancellationToken = default);
}

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
    long? MinOrderPaise);

public sealed record FulfillmentLocationSummary(Guid Id, Guid CityId, string Code, string Name, FulfillmentLocationStatus Status, string TimeZone);
