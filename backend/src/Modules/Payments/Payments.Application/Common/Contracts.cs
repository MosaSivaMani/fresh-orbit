using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.Payments.Domain.Settlements;
using FreshOrbit.Payments.Domain.Webhooks;

namespace FreshOrbit.Payments.Application.Common;

// ---- Requests ----

public sealed record CreateRefundRequest(long AmountPaise, RefundReason ReasonCode, RefundMethod Method, Guid? ComplaintId, string? UpiId, string? Notes);

public sealed record RejectRefundRequest(string? Reason);

public sealed record MarkRefundPaidRequest(string? Reference);

public sealed record CashCollectedRequest(long AmountPaise, CollectionMode Mode, string? UpiReference);

public sealed record CreateCashSettlementRequest(Guid RiderId, DateOnly Date, long ReceivedPaise, string? Note);

public sealed record DisputeCashSettlementRequest(string? Reason);

public sealed record SettleCashSettlementRequest(string? Note);

public sealed record ResolveIssueRequest(string? Note);

/// <summary>Development-only Fake outcome (G9): CAPTURED, FAILED, AUTHORIZED or AMOUNT_MISMATCH.</summary>
public sealed record SimulateFakePaymentRequest(string Outcome, long? AmountPaise);

// ---- Responses ----

/// <param name="KeyId">Public checkout key the client passes to the provider SDK.</param>
public sealed record InitiatePaymentResponse(Guid PaymentId, string Provider, string ProviderOrderId, long AmountPaise, string? KeyId, DateTimeOffset? ExpiresAt);

/// <summary>Result of initiate: <see cref="Created"/> false when an attempt was reused (200 instead of 201).</summary>
public sealed record InitiatePaymentResult(InitiatePaymentResponse Payment, bool Created);

public sealed record PaymentSummaryResponse(long PaidPaise, long RefundedPaise, long PendingRefundPaise, long RefundablePaise);

/// <summary>Customer view of a payment attempt.</summary>
public sealed record CustomerPaymentResponse(
    Guid Id,
    PaymentMethod Method,
    string Provider,
    PaymentStatus Status,
    long AmountPaise,
    string? ProviderOrderId,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? CapturedAt,
    DateTimeOffset? CollectedAt,
    string? FailureCode);

/// <summary>Customer view of a refund.</summary>
public sealed record CustomerRefundResponse(Guid Id, long AmountPaise, RefundReason ReasonCode, RefundMethod Method, RefundStatus Status, DateTimeOffset RequestedAt, DateTimeOffset? SucceededAt);

/// <summary>Staff view of a payment; <see cref="Version"/> is the ETag.</summary>
public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    PaymentMethod Method,
    string Provider,
    PaymentStatus Status,
    long AmountPaise,
    string Currency,
    string? ProviderOrderId,
    string? ProviderPaymentId,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? CapturedAt,
    long? CollectedPaise,
    DateTimeOffset? CollectedAt,
    Guid? CollectedByRiderId,
    CollectionMode? CollectionMode,
    string? CollectionReference,
    Guid? CashSettlementId,
    DateTimeOffset? OrderDeliveredAt,
    bool IsExcess,
    string? FailureCode,
    string? FailureMessage,
    PaymentSummaryResponse Summary,
    string Version,
    DateTimeOffset CreatedAt);

/// <summary>Staff view of a refund. <see cref="Version"/> is the row version of its payment (the aggregate root).</summary>
public sealed record RefundResponse(
    Guid Id,
    Guid OrderId,
    Guid PaymentId,
    Guid FulfillmentLocationId,
    long AmountPaise,
    RefundReason ReasonCode,
    RefundMethod Method,
    RefundStatus Status,
    Guid? RequestedBy,
    DateTimeOffset RequestedAt,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    Guid? ComplaintId,
    string? PayeeUpiId,
    string? Notes,
    string? FailureReason,
    string? ProviderRefundId,
    string? ManualReference,
    DateTimeOffset? SucceededAt,
    int AttemptCount,
    string Version,
    PaymentSummaryResponse PaymentSummary);

public sealed record OrderPaymentsResponse(Guid OrderId, IReadOnlyList<PaymentResponse> Payments, IReadOnlyList<RefundResponse> Refunds);

public sealed record WebhookEventResponse(
    Guid Id,
    string Provider,
    string ProviderEventId,
    string EventType,
    bool SignatureValid,
    WebhookEventStatus Status,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    string? Error,
    string PayloadSha256,
    bool PayloadTruncated);

public sealed record WebhookReceipt(bool Received, bool Duplicate);

public sealed record CashSettlementResponse(
    Guid Id,
    Guid RiderId,
    Guid FulfillmentLocationId,
    DateOnly Date,
    long ExpectedPaise,
    long ReceivedPaise,
    long DifferencePaise,
    CashSettlementStatus Status,
    int CollectionsCount,
    Guid? RecordedBy,
    Guid? SettledBy,
    DateTimeOffset? SettledAt,
    string? Note,
    string? DisputeReason,
    string Version);

public sealed record PaymentIssueResponse(
    Guid Id,
    IssueKind Kind,
    IssueSubjectType SubjectType,
    Guid SubjectId,
    Guid? OrderId,
    Guid? FulfillmentLocationId,
    string Details,
    IssueStatus Status,
    DateTimeOffset DetectedAt,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedBy,
    string? Note,
    string Version);
