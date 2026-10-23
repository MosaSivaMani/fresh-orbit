using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Payments.Domain.Events;

// Payloads use plain strings for codes so other modules can consume them without Payments enums
// (docs/product/payments.md §4 "Raised").

/// <summary>An online payment attempt was created at the provider.</summary>
public sealed record PaymentInitiated(Guid PaymentId, Guid OrderId, string Provider, long AmountPaise) : DomainEvent;

public sealed record PaymentAuthorized(Guid PaymentId, Guid OrderId) : DomainEvent;

/// <summary>Money was captured online. Ordering confirms the order or raises <c>PaymentReceivedForClosedOrder</c>.</summary>
public sealed record PaymentCaptured(Guid PaymentId, Guid OrderId, string Provider, long AmountPaise, DateTimeOffset CapturedAt) : DomainEvent;

/// <param name="HasOtherActiveAttempt">Another PENDING/AUTHORIZED attempt of the order has not expired (PAY-R11).</param>
public sealed record PaymentFailed(Guid PaymentId, Guid OrderId, string? FailureCode, bool HasOtherActiveAttempt) : DomainEvent;

/// <param name="Mode">CASH or UPI.</param>
public sealed record PaymentCollected(Guid PaymentId, Guid OrderId, long AmountPaise, string Mode, Guid? RiderId) : DomainEvent;

public sealed record PaymentCancelled(Guid PaymentId, Guid OrderId) : DomainEvent;

public sealed record RefundRequested(Guid RefundId, Guid OrderId, long AmountPaise, string ReasonCode) : DomainEvent;

/// <summary>Handled by the Payments Worker (provider refund for method ORIGINAL) and Notifications.</summary>
public sealed record RefundApproved(Guid RefundId, Guid OrderId, string Method) : DomainEvent;

public sealed record RefundSucceeded(Guid RefundId, Guid OrderId, long AmountPaise, string Method) : DomainEvent;

public sealed record RefundFailed(Guid RefundId, Guid OrderId, string? FailureReason) : DomainEvent;

public sealed record CashSettlementRecorded(Guid SettlementId, Guid RiderId, DateOnly Date, long DifferencePaise, string Status) : DomainEvent;

/// <param name="Details">JSON object describing the issue.</param>
public sealed record PaymentAlertRaised(Guid IssueId, string Kind, Guid? OrderId, Guid? FulfillmentLocationId, string Details) : DomainEvent;
