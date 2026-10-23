namespace FreshOrbit.Delivery.Public;

/// <summary>What other modules may ask the Delivery module.</summary>
public interface IDeliveryApi
{
    /// <summary>Open slots starting within [from, until) whose cutoff has not passed and that have capacity left.</summary>
    Task<IReadOnlyList<SlotInfo>> GetOpenSlotsAsync(Guid fulfillmentLocationId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken = default);

    /// <summary>
    /// Books one unit of slot capacity. Tracked on the shared DbContext and saved by the caller; never saves.
    /// Refusals (checked in this order): SLOT_NOT_AVAILABLE (missing, closed or another location),
    /// SLOT_CUTOFF_PASSED, SLOT_FULL. A refusal changes nothing, so the caller may retry from Read.
    /// </summary>
    Task<SlotBooking> BookSlotAsync(Guid slotId, Guid fulfillmentLocationId, CancellationToken cancellationToken = default);

    /// <summary>A slot of the location with its displayed status (checkout preview), or null when it does not exist there.</summary>
    Task<SlotView?> GetSlotAsync(Guid slotId, Guid fulfillmentLocationId, CancellationToken cancellationToken = default);

    /// <summary>Order tracking: shipment, rider and attempts; the delivery code only while OUT_FOR_DELIVERY. Null before the shipment exists.</summary>
    Task<ShipmentTracking?> GetShipmentForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Shipment facts for Payments (cash collection: order, rider, status).</summary>
    Task<ShipmentInfo?> GetShipmentAsync(Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>Rider facts for Payments (cash settlement); <c>TimeZone</c> is the rider's location time zone. Null when unknown.</summary>
    Task<RiderInfo?> GetRiderAsync(Guid riderId, CancellationToken cancellationToken = default);

    /// <summary>City readiness: the location has at least one active slot template.</summary>
    Task<bool> HasActiveSlotTemplateAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken = default);
}

public sealed record RiderInfo(Guid RiderId, Guid FulfillmentLocationId, string FullName, string Phone, bool IsActive, string TimeZone);

/// <summary>
/// Delivery limits held by the order policy (D12 <c>max_delivery_attempts</c>). Delivery does not use Settings; the
/// Settings module implements this port (same pattern as the city checks in <c>Locations.Public</c>, D4).
/// </summary>
public interface IDeliveryAttemptPolicy
{
    /// <summary>Delivery attempts allowed on the slot date for orders of the city (city policy, else the default policy).</summary>
    Task<int> GetMaxDeliveryAttemptsAsync(Guid cityId, CancellationToken cancellationToken = default);
}

public sealed record SlotInfo(Guid SlotId, DateTimeOffset StartAt, DateTimeOffset EndAt, DateTimeOffset CutoffAt, int Remaining);

/// <param name="RefusalCode">UPPER_SNAKE reason when not booked (e.g. SLOT_FULL, SLOT_CUTOFF_PASSED).</param>
public sealed record SlotBooking(bool Succeeded, Guid SlotId, DateTimeOffset? StartAt, DateTimeOffset? EndAt, string? RefusalCode);

/// <param name="Status">Displayed status: AVAILABLE, FULL, CUTOFF_PASSED or CLOSED.</param>
public sealed record SlotView(
    Guid SlotId,
    Guid FulfillmentLocationId,
    DateOnly SlotDate,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    DateTimeOffset CutoffAt,
    int Capacity,
    int BookedCount,
    string Status);

/// <param name="Status">PENDING, ASSIGNED, OUT_FOR_DELIVERY, DELIVERED, FAILED, RETURNED or CANCELLED.</param>
/// <param name="DeliveryCode">4-digit code for the customer; null unless OUT_FOR_DELIVERY.</param>
public sealed record ShipmentTracking(
    Guid ShipmentId,
    Guid OrderId,
    string Status,
    Guid? RiderId,
    string? RiderName,
    string? RiderPhone,
    DateTimeOffset? DispatchedAt,
    DateTimeOffset? DeliveredAt,
    string? DeliveryCode,
    IReadOnlyList<ShipmentAttemptInfo> Attempts)
{
    /// <summary>The order was packed with a cold box.</summary>
    public bool ColdBoxUsed { get; init; }
}

/// <param name="Outcome">DELIVERED, CUSTOMER_UNAVAILABLE, ADDRESS_NOT_FOUND, REFUSED or OTHER.</param>
public sealed record ShipmentAttemptInfo(int AttemptNo, string Outcome, DateTimeOffset AttemptedAt);

/// <param name="Status">PENDING, ASSIGNED, OUT_FOR_DELIVERY, DELIVERED, FAILED, RETURNED or CANCELLED.</param>
public sealed record ShipmentInfo(
    Guid ShipmentId,
    Guid OrderId,
    Guid FulfillmentLocationId,
    Guid DeliverySlotId,
    Guid? RiderId,
    string Status,
    DateTimeOffset? DeliveredAt,
    long? CashCollectedPaise);
