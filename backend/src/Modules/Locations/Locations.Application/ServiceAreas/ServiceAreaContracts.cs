namespace FreshOrbit.Locations.Application.ServiceAreas;

public sealed record CreateServiceAreaRequest(
    Guid CityId,
    Guid FulfillmentLocationId,
    string? Name,
    long DeliveryFeePaise,
    long? FreeDeliveryAbovePaise,
    long MinOrderPaise);

public sealed record UpdateServiceAreaRequest(
    string? Name,
    long DeliveryFeePaise,
    long? FreeDeliveryAbovePaise,
    long MinOrderPaise,
    bool IsActive);

public sealed record AddPincodesRequest(IReadOnlyList<string?>? Pincodes);

public sealed record ServiceAreaResponse(
    Guid Id,
    Guid CityId,
    Guid FulfillmentLocationId,
    string Name,
    long DeliveryFeePaise,
    long? FreeDeliveryAbovePaise,
    long MinOrderPaise,
    bool IsActive,
    int ActivePincodeCount,
    string Version);

public sealed record PincodeResponse(string Pincode, bool IsActive, DateTimeOffset AddedAt);

/// <summary>Result of AddPincodes: what was added (pincodes already active in the area are skipped).</summary>
public sealed record AddPincodesResponse(Guid ServiceAreaId, IReadOnlyList<string> Added, int ActivePincodeCount, string Version);
