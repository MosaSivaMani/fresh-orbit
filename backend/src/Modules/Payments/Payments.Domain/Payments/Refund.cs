using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Payments.Domain.Payments;

/// <summary>
/// Money going back for one payment. A child of <see cref="Payment"/>: it is created and changed only through the
/// aggregate, which keeps the payment's refund summary columns (and so its row version) in step.
/// </summary>
public sealed class Refund : Entity
{
    private Refund()
    {
        IdempotencyKey = string.Empty;
    }

    internal Refund(
        Guid paymentId,
        Guid orderId,
        Guid fulfillmentLocationId,
        long amountPaise,
        RefundReason reasonCode,
        RefundMethod method,
        RefundStatus status,
        Guid? requestedBy,
        DateTimeOffset requestedAt)
    {
        PaymentId = paymentId;
        OrderId = orderId;
        FulfillmentLocationId = fulfillmentLocationId;
        AmountPaise = amountPaise;
        ReasonCode = reasonCode;
        Method = method;
        Status = status;
        RequestedBy = requestedBy;
        RequestedAt = requestedAt;
        IdempotencyKey = $"refund:{Id}";
    }

    public Guid PaymentId { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid FulfillmentLocationId { get; private set; }

    public long AmountPaise { get; private set; }

    public RefundReason ReasonCode { get; private set; }

    public RefundMethod Method { get; private set; }

    public RefundStatus Status { get; private set; }

    /// <summary>Provider idempotency key <c>refund:{id}</c>, fixed at creation (PAY-R16).</summary>
    public string IdempotencyKey { get; private set; }

    public string? ProviderRefundId { get; private set; }

    /// <summary>Staff user id; null for a system refund.</summary>
    public Guid? RequestedBy { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>Approver user id; null for a system (auto-approved) refund.</summary>
    public Guid? ApprovedBy { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public Guid? ComplaintId { get; private set; }

    /// <summary>Natural idempotency guard of system/handler refunds (e.g. <c>OrderPacked:{orderId}:WEIGHT_SHORTFALL</c>).</summary>
    public string? SourceReference { get; private set; }

    public string? PayeeUpiId { get; private set; }

    public string? Notes { get; private set; }

    public string? FailureReason { get; private set; }

    public string? ManualReference { get; private set; }

    public DateTimeOffset? SucceededAt { get; private set; }

    public Guid? SucceededBy { get; private set; }

    public int AttemptCount { get; private set; }

    public bool IsSystemRequested => RequestedBy is null;

    /// <summary>Counts against the refundable amount (PAY-R12): everything except FAILED.</summary>
    public bool HoldsAmount => Status is not RefundStatus.Failed;

    public bool IsPending => Status is RefundStatus.Requested or RefundStatus.Approved or RefundStatus.Processing;

    internal void SetDetails(Guid? complaintId, string? sourceReference, string? payeeUpiId, string? notes)
    {
        ComplaintId = complaintId;
        SourceReference = sourceReference;
        PayeeUpiId = payeeUpiId;
        Notes = notes;
    }

    internal void Approve(Guid? approvedBy, DateTimeOffset at)
    {
        Status = RefundStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = at;
    }

    internal void StartProcessing()
    {
        Status = RefundStatus.Processing;
        AttemptCount++;
    }

    internal void SetProviderRefundId(string providerRefundId) => ProviderRefundId = providerRefundId;

    internal void Succeed(DateTimeOffset at, Guid? by, string? manualReference)
    {
        Status = RefundStatus.Succeeded;
        SucceededAt = at;
        SucceededBy = by;
        ManualReference = manualReference;
    }

    internal void Fail(string? reason)
    {
        Status = RefundStatus.Failed;
        FailureReason = reason;
    }
}
