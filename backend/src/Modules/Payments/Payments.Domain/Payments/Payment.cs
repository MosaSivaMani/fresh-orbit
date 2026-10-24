using FreshOrbit.Payments.Public.Events;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Payments.Domain.Payments;

/// <summary>
/// One payment attempt with one provider for an order, plus the refunds of that payment.
/// Every refund change also changes <see cref="RefundRevision"/> and the refund summary columns, so the payment
/// row version moves and two concurrent refund commands can never both win (PAY-R12, decision D15).
/// </summary>
public sealed class Payment : AggregateRoot
{
    public const int MaxManualReferenceLength = 64;
    public const int MinManualReferenceLength = 4;

    private readonly List<Refund> _refunds = [];

    private Payment()
    {
        Provider = string.Empty;
        Currency = PaymentProviders.Inr;
    }

    private Payment(Guid id, Guid orderId, Guid customerId, Guid fulfillmentLocationId, PaymentMethod method, string provider, long amountPaise)
        : base(id)
    {
        OrderId = orderId;
        CustomerId = customerId;
        FulfillmentLocationId = fulfillmentLocationId;
        Method = method;
        Provider = provider;
        AmountPaise = amountPaise;
        Currency = PaymentProviders.Inr;
        Status = PaymentStatus.Pending;
    }

    public Guid OrderId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid FulfillmentLocationId { get; private set; }

    public PaymentMethod Method { get; private set; }

    /// <summary>Adapter key, see <see cref="PaymentProviders"/>.</summary>
    public string Provider { get; private set; }

    /// <summary>Amount expected for this attempt (for COD: the current amount due).</summary>
    public long AmountPaise { get; private set; }

    public string Currency { get; private set; }

    public PaymentStatus Status { get; private set; }

    public string? ProviderOrderId { get; private set; }

    public string? ProviderPaymentId { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? AuthorizedAt { get; private set; }

    public DateTimeOffset? CapturedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Amount actually collected on delivery (may differ from <see cref="AmountPaise"/> via the deliver path).</summary>
    public long? CollectedPaise { get; private set; }

    public DateTimeOffset? CollectedAt { get; private set; }

    public Guid? CollectedByRiderId { get; private set; }

    public CollectionMode? CollectionMode { get; private set; }

    public string? CollectionReference { get; private set; }

    public Guid? CashSettlementId { get; private set; }

    public DateTimeOffset? OrderDeliveredAt { get; private set; }

    /// <summary>A second capture on an order already paid by another attempt (schema S3); refunded in full.</summary>
    public bool IsExcess { get; private set; }

    public string? FailureCode { get; private set; }

    public string? FailureMessage { get; private set; }

    /// <summary>
    /// When the attempt was created (G12): the <c>created_at</c> audit column every table carries, stamped by the
    /// persistence layer on the first save (default until then). Never set by the domain.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Σ SUCCEEDED refunds.</summary>
    public long RefundedPaise { get; private set; }

    /// <summary>Σ REQUESTED, APPROVED and PROCESSING refunds.</summary>
    public long RefundPendingPaise { get; private set; }

    /// <summary>Incremented by every refund change so the payment row (and its <c>xmin</c>) is always updated.</summary>
    public int RefundRevision { get; private set; }

    public IReadOnlyList<Refund> Refunds => _refunds;

    public bool IsPaid => Status is PaymentStatus.Captured or PaymentStatus.Collected;

    public bool IsDeliveryCollected => Method is PaymentMethod.Cod or PaymentMethod.UpiOnDelivery;

    public bool IsOpenAttempt => Status is PaymentStatus.Pending or PaymentStatus.Authorized;

    /// <summary>Captured amount, or the amount collected on delivery; 0 when nothing was paid.</summary>
    public long PaidPaise => Status switch
    {
        PaymentStatus.Captured => AmountPaise,
        PaymentStatus.Collected => CollectedPaise ?? AmountPaise,
        _ => 0,
    };

    /// <summary>paid − Σ refunds that still hold an amount (PAY-R12).</summary>
    public long RefundablePaise => PaidPaise - _refunds.Where(r => r.HoldsAmount).Sum(r => r.AmountPaise);

    /// <summary>A PENDING delivery payment (COD or UPI on delivery) created on <c>OrderPlaced</c> (PAY-R3).</summary>
    public static Payment CreateForDelivery(Guid orderId, Guid customerId, Guid fulfillmentLocationId, PaymentMethod method, long amountDuePaise)
    {
        if (method is not (PaymentMethod.Cod or PaymentMethod.UpiOnDelivery))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "Only COD and UPI on delivery are collected on delivery.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(amountDuePaise);
        var provider = method == PaymentMethod.Cod ? PaymentProviders.Cash : PaymentProviders.UpiOnDelivery;
        return new Payment(IdGenerator.NewId(), orderId, customerId, fulfillmentLocationId, method, provider, amountDuePaise);
    }

    /// <summary>A PENDING online attempt whose provider order already exists (PAY-R5).</summary>
    public static Payment CreateOnlineAttempt(
        Guid paymentId,
        Guid orderId,
        Guid customerId,
        Guid fulfillmentLocationId,
        string provider,
        long amountPaise,
        string providerOrderId,
        DateTimeOffset? expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerOrderId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountPaise);
        var payment = new Payment(paymentId, orderId, customerId, fulfillmentLocationId, PaymentMethod.Online, provider, amountPaise)
        {
            ProviderOrderId = providerOrderId,
            ExpiresAt = expiresAt,
        };
        payment.Raise(new PaymentInitiated(paymentId, orderId, provider, amountPaise));
        return payment;
    }

    /// <summary>PENDING → AUTHORIZED. Returns false when nothing changed (already authorized or further).</summary>
    public bool Authorize(string? providerPaymentId, DateTimeOffset at)
    {
        if (Status != PaymentStatus.Pending)
        {
            return false;
        }

        Status = PaymentStatus.Authorized;
        AuthorizedAt = at;
        ProviderPaymentId ??= providerPaymentId;
        Raise(new PaymentAuthorized(Id, OrderId));
        return true;
    }

    /// <summary>Whether a captured amount and currency match this attempt (PAY-R9).</summary>
    public bool Matches(long amountPaise, string? currency)
        => amountPaise == AmountPaise && string.Equals(currency ?? PaymentProviders.Inr, PaymentProviders.Inr, StringComparison.Ordinal);

    /// <summary>
    /// PENDING/AUTHORIZED (or a late capture on FAILED/CANCELLED) → CAPTURED. The caller checks the amount first
    /// (<see cref="Matches"/>). Returns false when already captured.
    /// </summary>
    /// <param name="orderAlreadyPaid">Another attempt of the order is already paid: this capture is excess money.</param>
    public bool Capture(string? providerPaymentId, DateTimeOffset at, bool orderAlreadyPaid)
    {
        if (Status == PaymentStatus.Captured)
        {
            return false;
        }

        if (Status is PaymentStatus.Collected || Method != PaymentMethod.Online)
        {
            throw new ConflictException(PaymentsErrors.InvalidStatusTransition, "Only an online attempt can be captured.");
        }

        Status = PaymentStatus.Captured;
        CapturedAt = at;
        ProviderPaymentId ??= providerPaymentId;
        IsExcess = orderAlreadyPaid;
        FailureCode = null;
        FailureMessage = null;
        Raise(new PaymentCaptured(Id, OrderId, Provider, AmountPaise, at));
        return true;
    }

    /// <summary>PENDING/AUTHORIZED → FAILED. Returns false when the attempt is no longer open.</summary>
    public bool Fail(string? providerPaymentId, string? failureCode, string? failureMessage, bool hasOtherActiveAttempt)
    {
        if (!IsOpenAttempt)
        {
            return false;
        }

        Status = PaymentStatus.Failed;
        ProviderPaymentId ??= providerPaymentId;
        FailureCode = failureCode;
        FailureMessage = failureMessage is { Length: > 500 } ? failureMessage[..500] : failureMessage;
        Raise(new PaymentFailed(Id, OrderId, failureCode, hasOtherActiveAttempt));
        return true;
    }

    /// <summary>PENDING/AUTHORIZED → CANCELLED (PAY-R6). Returns false when the attempt is no longer open.</summary>
    public bool Cancel(DateTimeOffset at)
    {
        if (!IsOpenAttempt)
        {
            return false;
        }

        Status = PaymentStatus.Cancelled;
        CancelledAt = at;
        Raise(new PaymentCancelled(Id, OrderId));
        return true;
    }

    /// <summary>
    /// Records a COD / UPI-on-delivery collection (PAY-R4). With <paramref name="requireExactAmount"/> (staff route) a
    /// different amount is refused with <c>CASH_AMOUNT_MISMATCH</c>; the deliver path records what was given.
    /// </summary>
    public void RecordCollection(long amountPaise, CollectionMode mode, string? reference, Guid? riderId, DateTimeOffset at, bool requireExactAmount)
    {
        if (!IsDeliveryCollected)
        {
            throw new ConflictException(PaymentsErrors.InvalidStatusTransition, "Only a cash or UPI-on-delivery payment is collected on delivery.");
        }

        if (Status == PaymentStatus.Collected)
        {
            throw new ConflictException(PaymentsErrors.CashAlreadyCollected, "The collection for this order is already recorded.");
        }

        if (Status != PaymentStatus.Pending)
        {
            throw new ConflictException(PaymentsErrors.InvalidStatusTransition, $"A {Status.ToCode()} payment cannot be collected.");
        }

        var errors = new List<FieldError>();
        if (amountPaise <= 0)
        {
            errors.Add(new FieldError("amountPaise", PaymentsErrors.AmountInvalid, "Amount paise must be greater than zero."));
        }
        else if (requireExactAmount && amountPaise != AmountPaise)
        {
            errors.Add(new FieldError("amountPaise", PaymentsErrors.CashAmountMismatch, $"Amount paise must equal the amount due of {AmountPaise} paise."));
        }

        if (mode == Payments.CollectionMode.Upi && string.IsNullOrWhiteSpace(reference))
        {
            errors.Add(new FieldError("upiReference", PaymentsErrors.UpiReferenceRequired, "UPI reference is required for a UPI collection."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        Status = PaymentStatus.Collected;
        CollectedPaise = amountPaise;
        CollectionMode = mode;
        CollectionReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        CollectedByRiderId = riderId;
        CollectedAt = at;
        Raise(new PaymentCollected(Id, OrderId, amountPaise, mode.ToCode(), riderId));
    }

    /// <summary>The amount due changed after packing or an adjustment (PAY-R8). Only a PENDING delivery payment follows.</summary>
    public bool UpdateAmountDue(long amountDuePaise)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountDuePaise);
        if (!IsDeliveryCollected || Status != PaymentStatus.Pending || AmountPaise == amountDuePaise)
        {
            return false;
        }

        AmountPaise = amountDuePaise;
        return true;
    }

    public void MarkOrderDelivered(DateTimeOffset deliveredAt) => OrderDeliveredAt ??= deliveredAt;

    public void AssignToSettlement(Guid settlementId)
    {
        if (Status != PaymentStatus.Collected || CollectionMode != Payments.CollectionMode.Cash || CashSettlementId is not null)
        {
            throw new InvalidOperationException("Only an unsettled cash collection can be settled.");
        }

        CashSettlementId = settlementId;
    }

    /// <summary>
    /// A staff or complaint refund (PAY-R12, R13), REQUESTED for approval by someone else.
    /// </summary>
    /// <param name="providerSupportsRefunds">The payment's provider can refund to the original instrument.</param>
    public Refund RequestRefund(RefundDraft draft, bool providerSupportsRefunds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(draft);
        EnsurePaid();
        var errors = new List<FieldError>();
        if (draft.AmountPaise <= 0)
        {
            errors.Add(new FieldError("amountPaise", PaymentsErrors.AmountInvalid, "Amount paise must be greater than zero."));
        }

        if (draft.Method == RefundMethod.Original && !providerSupportsRefunds)
        {
            errors.Add(new FieldError("method", PaymentsErrors.RefundMethodNotAllowed, "Method ORIGINAL is only allowed for online payments; choose UPI, BANK_TRANSFER or CASH."));
        }

        if (draft.Method == RefundMethod.Upi && string.IsNullOrWhiteSpace(draft.PayeeUpiId))
        {
            errors.Add(new FieldError("upiId", PaymentsErrors.UpiIdRequired, "UPI id is required for a UPI refund."));
        }

        if (draft.ReasonCode == RefundReason.Complaint && draft.ComplaintId is null)
        {
            errors.Add(new FieldError("complaintId", PaymentsErrors.ComplaintRequired, "Complaint id is required for a COMPLAINT refund."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        if (draft.AmountPaise > RefundablePaise)
        {
            throw new DomainException(PaymentsErrors.RefundExceedsPayment, $"Amount paise must be at most the refundable {RefundablePaise} paise.", "amountPaise");
        }

        var refund = AddRefund(draft, draft.AmountPaise, RefundStatus.Requested, now);
        Raise(new RefundRequested(refund.Id, OrderId, refund.AmountPaise, refund.ReasonCode.ToCode()));
        return refund;
    }

    /// <summary>
    /// A system refund (PAY-R15): method ORIGINAL, amount capped at the refundable amount, created APPROVED unless
    /// <paramref name="requiresApproval"/>. Returns null when nothing is refundable.
    /// </summary>
    public SystemRefundResult? CreateSystemRefund(RefundReason reason, long amountPaise, string sourceReference, DateTimeOffset now, bool requiresApproval = false, Guid? requestedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);
        if (!IsPaid || amountPaise <= 0 || _refunds.Any(r => r.SourceReference == sourceReference))
        {
            return null;
        }

        var refundable = RefundablePaise;
        if (refundable <= 0)
        {
            return new SystemRefundResult(null, amountPaise, 0);
        }

        var amount = Math.Min(amountPaise, refundable);
        var draft = new RefundDraft(amount, reason, RefundMethod.Original, requestedBy, null, null, null, sourceReference);
        if (requiresApproval)
        {
            var requested = AddRefund(draft, amount, RefundStatus.Requested, now);
            Raise(new RefundRequested(requested.Id, OrderId, amount, reason.ToCode()));
            return new SystemRefundResult(requested, amountPaise, amount);
        }

        var refund = AddRefund(draft, amount, RefundStatus.Approved, now);
        refund.Approve(null, now);
        Raise(new RefundApproved(refund.Id, OrderId, refund.Method.ToCode()) { AmountPaise = refund.AmountPaise });
        return new SystemRefundResult(refund, amountPaise, amount);
    }

    /// <summary>REQUESTED → APPROVED by someone other than the requester (PAY-R14).</summary>
    public Refund ApproveRefund(Guid refundId, Guid approverId, DateTimeOffset now)
    {
        var refund = GetRequestedRefund(refundId, approverId);
        refund.Approve(approverId, now);
        Touch();
        Raise(new RefundApproved(refund.Id, OrderId, refund.Method.ToCode()) { AmountPaise = refund.AmountPaise });
        return refund;
    }

    /// <summary>REQUESTED → FAILED with <c>REJECTED: reason</c> (PAY-R14).</summary>
    public Refund RejectRefund(Guid refundId, Guid rejecterId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new RefusalException(new FieldError("reason", PaymentsErrors.ReasonRequired, "Reason is required to reject a refund."));
        }

        var refund = GetRequestedRefund(refundId, rejecterId);
        refund.Fail($"REJECTED: {reason.Trim()}");
        Touch();
        Raise(new RefundFailed(refund.Id, OrderId, refund.FailureReason));
        return refund;
    }

    /// <summary>
    /// Worker step 1 (PAY-R16): APPROVED → PROCESSING, or a re-entry on PROCESSING without a provider refund id.
    /// Returns false when there is nothing to send to the provider.
    /// </summary>
    public bool StartRefundProcessing(Guid refundId)
    {
        var refund = GetRefund(refundId);
        if (refund.Method != RefundMethod.Original)
        {
            return false;
        }

        var startable = refund.Status == RefundStatus.Approved
            || (refund.Status == RefundStatus.Processing && refund.ProviderRefundId is null);
        if (!startable)
        {
            return false;
        }

        refund.StartProcessing();
        Touch();
        return true;
    }

    /// <summary>Worker step 2 (PAY-R16): stores the provider's answer.</summary>
    public void ApplyProviderRefundResult(Guid refundId, string? providerRefundId, ProviderRefundStatus status, string? failureReason, DateTimeOffset now)
    {
        var refund = GetRefund(refundId);
        if (refund.Status != RefundStatus.Processing)
        {
            return;
        }

        if (providerRefundId is not null && refund.ProviderRefundId is null)
        {
            refund.SetProviderRefundId(providerRefundId);
            Touch();
        }

        switch (status)
        {
            case ProviderRefundStatus.Processed:
                CompleteRefund(refundId, now);
                break;
            case ProviderRefundStatus.Failed:
                FailRefund(refundId, failureReason ?? "PROVIDER_FAILED");
                break;
        }
    }

    /// <summary>PROCESSING → SUCCEEDED (provider result, webhook, reconciliation). Returns false when already final.</summary>
    public bool CompleteRefund(Guid refundId, DateTimeOffset now)
    {
        var refund = GetRefund(refundId);
        if (refund.Status != RefundStatus.Processing)
        {
            return false;
        }

        refund.Succeed(now, null, null);
        Touch();
        Raise(new RefundSucceeded(refund.Id, OrderId, refund.AmountPaise, refund.Method.ToCode()));
        return true;
    }

    /// <summary>PROCESSING → FAILED (permanent provider failure). Returns false when already final.</summary>
    public bool FailRefund(Guid refundId, string reason)
    {
        var refund = GetRefund(refundId);
        if (refund.Status != RefundStatus.Processing)
        {
            return false;
        }

        refund.Fail(reason);
        Touch();
        Raise(new RefundFailed(refund.Id, OrderId, reason));
        return true;
    }

    /// <summary>A manual UPI / bank / cash refund was paid (PAY-R17).</summary>
    public Refund MarkRefundPaid(Guid refundId, string reference, Guid by, DateTimeOffset now)
    {
        var refund = GetRefund(refundId);
        if (refund.Method == RefundMethod.Original || refund.Status != RefundStatus.Approved)
        {
            throw new ConflictException(PaymentsErrors.InvalidStatusTransition, "Only an APPROVED UPI, BANK_TRANSFER or CASH refund can be marked paid.");
        }

        var trimmed = reference?.Trim() ?? string.Empty;
        if (trimmed.Length is < MinManualReferenceLength or > MaxManualReferenceLength)
        {
            throw new RefusalException(new FieldError("reference", PaymentsErrors.ReferenceInvalid, $"Reference must be {MinManualReferenceLength} to {MaxManualReferenceLength} characters."));
        }

        refund.Succeed(now, by, trimmed);
        Touch();
        Raise(new RefundSucceeded(refund.Id, OrderId, refund.AmountPaise, refund.Method.ToCode()));
        return refund;
    }

    public Refund GetRefund(Guid refundId)
        => _refunds.FirstOrDefault(r => r.Id == refundId)
            ?? throw new NotFoundException("REFUND_NOT_FOUND", "No refund has this id.");

    private Refund GetRequestedRefund(Guid refundId, Guid actorId)
    {
        var refund = GetRefund(refundId);
        if (refund.Status != RefundStatus.Requested)
        {
            throw new ConflictException(PaymentsErrors.RefundNotApprovable, "Only a REQUESTED refund can be approved or rejected.");
        }

        if (refund.RequestedBy == actorId)
        {
            throw new DomainException(PaymentsErrors.RefundSelfApproval, "The requester of a refund cannot approve or reject it; ask another approver.");
        }

        return refund;
    }

    private void EnsurePaid()
    {
        if (!IsPaid)
        {
            throw new DomainException(PaymentsErrors.NoCapturedPayment, "The order has no captured or collected payment to refund.");
        }
    }

    private Refund AddRefund(RefundDraft draft, long amountPaise, RefundStatus status, DateTimeOffset now)
    {
        var refund = new Refund(Id, OrderId, FulfillmentLocationId, amountPaise, draft.ReasonCode, draft.Method, status, draft.RequestedBy, now);
        refund.SetDetails(draft.ComplaintId, draft.SourceReference, string.IsNullOrWhiteSpace(draft.PayeeUpiId) ? null : draft.PayeeUpiId.Trim(), draft.Notes);
        _refunds.Add(refund);
        Touch();
        return refund;
    }

    private void Touch()
    {
        RefundRevision++;
        RefundedPaise = _refunds.Where(r => r.Status == RefundStatus.Succeeded).Sum(r => r.AmountPaise);
        RefundPendingPaise = _refunds.Where(r => r.IsPending).Sum(r => r.AmountPaise);
    }
}

/// <summary>What a refund request asks for.</summary>
/// <param name="RequestedBy">Staff user id; null for the system.</param>
public sealed record RefundDraft(
    long AmountPaise,
    RefundReason ReasonCode,
    RefundMethod Method,
    Guid? RequestedBy,
    Guid? ComplaintId,
    string? PayeeUpiId,
    string? Notes,
    string? SourceReference);

/// <summary>Outcome of a system refund: the refund (null when nothing was refundable) and whether it was capped.</summary>
public sealed record SystemRefundResult(Refund? Refund, long RequestedPaise, long GrantedPaise)
{
    public bool WasCapped => GrantedPaise < RequestedPaise;
}
