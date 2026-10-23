using FreshOrbit.Payments.Public.Events;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Payments.Domain.Reconciliation;

/// <summary>What needs a person's attention. Stored as UPPER_SNAKE codes.</summary>
public enum IssueKind
{
    AmountMismatch,
    MissedWebhook,
    CollectionDifference,
    RefundCapped,
    RefundStuck,
    DeliveredNotCollected,
    UnsettledCash,
    SettlementAged,
    CashDifference,
    WebhookError,
    RefundMethodRequired,
}

/// <summary>Stored as OPEN / AUTO_RESOLVED / RESOLVED.</summary>
public enum IssueStatus
{
    Open,
    AutoResolved,
    Resolved,
}

/// <summary>Stored as PAYMENT / REFUND / SETTLEMENT / WEBHOOK_EVENT / ORDER.</summary>
public enum IssueSubjectType
{
    Payment,
    Refund,
    Settlement,
    WebhookEvent,
    Order,
}

/// <summary>
/// A payment discrepancy found by a webhook, a handler or the reconciliation job (PAY-R22). At most one OPEN issue per
/// (kind, subject) — a partial unique index.
/// </summary>
public sealed class ReconciliationIssue : AggregateRoot
{
    private ReconciliationIssue()
    {
        Details = "{}";
    }

    private ReconciliationIssue(IssueKind kind, IssueSubjectType subjectType, Guid subjectId, Guid? orderId, Guid? fulfillmentLocationId, string details, DateTimeOffset detectedAt)
    {
        Kind = kind;
        SubjectType = subjectType;
        SubjectId = subjectId;
        OrderId = orderId;
        FulfillmentLocationId = fulfillmentLocationId;
        Details = details;
        DetectedAt = detectedAt;
        Status = IssueStatus.Open;
    }

    public IssueKind Kind { get; private set; }

    public IssueSubjectType SubjectType { get; private set; }

    public Guid SubjectId { get; private set; }

    public Guid? OrderId { get; private set; }

    public Guid? FulfillmentLocationId { get; private set; }

    /// <summary>JSON object (jsonb).</summary>
    public string Details { get; private set; }

    public IssueStatus Status { get; private set; }

    public DateTimeOffset DetectedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public Guid? ResolvedBy { get; private set; }

    public string? Note { get; private set; }

    /// <summary>A new OPEN issue; raises <see cref="PaymentAlertRaised"/> for staff with <c>payments.view</c>.</summary>
    public static ReconciliationIssue Open(IssueKind kind, IssueSubjectType subjectType, Guid subjectId, Guid? orderId, Guid? fulfillmentLocationId, string details, DateTimeOffset detectedAt)
    {
        var issue = new ReconciliationIssue(kind, subjectType, subjectId, orderId, fulfillmentLocationId, details, detectedAt);
        issue.Raise(new PaymentAlertRaised(issue.Id, kind.ToCode(), orderId, fulfillmentLocationId, details));
        return issue;
    }

    /// <summary>An issue found and fixed by the same run (e.g. a missed webhook applied by reconciliation); no alert.</summary>
    public static ReconciliationIssue Recorded(IssueKind kind, IssueSubjectType subjectType, Guid subjectId, Guid? orderId, Guid? fulfillmentLocationId, string details, DateTimeOffset detectedAt)
    {
        var issue = new ReconciliationIssue(kind, subjectType, subjectId, orderId, fulfillmentLocationId, details, detectedAt);
        issue.AutoResolve(detectedAt);
        return issue;
    }

    /// <summary>OPEN → AUTO_RESOLVED when a later run finds the condition gone.</summary>
    public void AutoResolve(DateTimeOffset at)
    {
        if (Status != IssueStatus.Open)
        {
            return;
        }

        Status = IssueStatus.AutoResolved;
        ResolvedAt = at;
    }

    /// <summary>OPEN → RESOLVED by staff with a note.</summary>
    public void Resolve(Guid by, string note, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new RefusalException(new FieldError("note", PaymentsErrors.ReasonRequired, "Note is required to resolve an issue."));
        }

        if (Status != IssueStatus.Open)
        {
            throw new ConflictException(PaymentsErrors.InvalidStatusTransition, "Only an OPEN issue can be resolved.");
        }

        Status = IssueStatus.Resolved;
        ResolvedBy = by;
        ResolvedAt = at;
        Note = note.Trim();
    }
}
