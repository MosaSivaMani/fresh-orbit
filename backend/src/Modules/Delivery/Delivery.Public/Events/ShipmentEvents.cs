using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Delivery.Public.Events;

public sealed record RiderAssigned(Guid ShipmentId, Guid OrderId, Guid FulfillmentLocationId, Guid RiderId, Guid? PreviousRiderId) : DomainEvent;

/// <summary>Ordering moves the order PACKED → OUT_FOR_DELIVERY.</summary>
public sealed record ShipmentDispatched(
    Guid ShipmentId,
    Guid OrderId,
    Guid FulfillmentLocationId,
    Guid RiderId,
    DateTimeOffset DispatchedAt,
    decimal DispatchTemperatureC,
    bool TemperatureWarning,
    bool ColdBoxUsed) : DomainEvent;

/// <summary>Ordering moves the order to DELIVERED and issues the invoice; Payments records cash given at the door.</summary>
public sealed record ShipmentDelivered(
    Guid ShipmentId,
    Guid OrderId,
    Guid FulfillmentLocationId,
    Guid RiderId,
    DateTimeOffset DeliveredAt,
    long? CashCollectedPaise,
    string? CashMode,
    string? UpiReference) : DomainEvent;

/// <summary>Ordering moves the order to DELIVERY_FAILED.</summary>
public sealed record DeliveryAttemptFailed(
    Guid ShipmentId,
    Guid OrderId,
    Guid FulfillmentLocationId,
    int AttemptNo,
    string Outcome,
    string? Notes,
    DateTimeOffset AttemptedAt) : DomainEvent;

/// <summary>Ordering moves the order DELIVERY_FAILED → OUT_FOR_DELIVERY.</summary>
public sealed record ShipmentReattempted(Guid ShipmentId, Guid OrderId, Guid FulfillmentLocationId, Guid RiderId, int AttemptNo) : DomainEvent;

/// <summary>Ordering moves the order to RETURNED.</summary>
public sealed record ShipmentReturned(Guid ShipmentId, Guid OrderId, Guid FulfillmentLocationId, DateTimeOffset ReturnedAt) : DomainEvent;

/// <summary>The order was cancelled after dispatch; the rider must bring the box back (D12).</summary>
public sealed record ShipmentRecallRequired(Guid ShipmentId, Guid OrderId, Guid FulfillmentLocationId, Guid? RiderId) : DomainEvent;
