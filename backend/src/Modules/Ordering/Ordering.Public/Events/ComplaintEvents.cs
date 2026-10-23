using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Ordering.Public.Events;

/// <summary>A customer raised a complaint (staff alert).</summary>
public sealed record ComplaintRaised(
    Guid ComplaintId,
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid? OrderItemId,
    string Kind,
    DateTimeOffset At) : DomainEvent;

/// <summary>Staff decided a complaint; APPROVED + REFUND makes Payments create a COMPLAINT refund request.</summary>
public sealed record ComplaintDecided(
    Guid ComplaintId,
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid? OrderItemId,
    string Status,
    string Resolution,
    long? RefundAmountPaise,
    string? RefundMethod,
    Guid? DecidedBy,
    DateTimeOffset At) : DomainEvent;
