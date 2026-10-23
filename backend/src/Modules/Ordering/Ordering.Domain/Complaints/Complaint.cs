using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Ordering.Domain.Complaints;

/// <summary>Stored as MISSING / DAMAGED / QUALITY / WRONG_ITEM / LATE.</summary>
public enum ComplaintKind
{
    Missing,
    Damaged,
    Quality,
    WrongItem,
    Late,
}

/// <summary>Stored as OPEN / APPROVED / REJECTED / RESOLVED.</summary>
public enum ComplaintStatus
{
    Open,
    Approved,
    Rejected,
    Resolved,
}

/// <summary>Stored as REFUND / REPLACEMENT / NONE.</summary>
public enum ComplaintResolution
{
    Refund,
    Replacement,
    None,
}

/// <summary>The order facts a complaint is raised against.</summary>
public sealed record ComplaintOrderFacts(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    DateTimeOffset DeliveredAt);

/// <summary>
/// A customer complaint about a delivered order (<c>ordering.complaints</c>, row version <c>xmin</c>). Decides the
/// complaint window, the one-open-per-line rule, the decision and its resolution (ORD-R54..R57).
/// </summary>
public sealed class Complaint : AggregateRoot
{
    public const int MinDescriptionLength = 10;
    public const int MaxDescriptionLength = 1000;

    private Complaint()
    {
        OrderNumber = Description = string.Empty;
    }

    private Complaint(ComplaintOrderFacts order, Guid? orderItemId, ComplaintKind kind, string description, DateTimeOffset at)
    {
        OrderId = order.OrderId;
        OrderNumber = order.OrderNumber;
        CustomerId = order.CustomerId;
        FulfillmentLocationId = order.FulfillmentLocationId;
        OrderItemId = orderItemId;
        Kind = kind;
        Description = description;
        Status = ComplaintStatus.Open;
        RaisedAt = at;
    }

    public Guid OrderId { get; private set; }

    public string OrderNumber { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid FulfillmentLocationId { get; private set; }

    /// <summary>The line complained about; null for LATE (order level).</summary>
    public Guid? OrderItemId { get; private set; }

    public ComplaintKind Kind { get; private set; }

    public string Description { get; private set; }

    public ComplaintStatus Status { get; private set; }

    public ComplaintResolution? Resolution { get; private set; }

    public long? RefundAmountPaise { get; private set; }

    public string? DecisionNote { get; private set; }

    public Guid? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public Guid? ResolvedBy { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public DateTimeOffset RaisedAt { get; private set; }

    /// <summary>
    /// Raises a complaint (ORD-R54, R55): within <paramref name="windowHours"/> of delivery, a line unless LATE, one
    /// OPEN/APPROVED complaint per line (per order for LATE), description 10–1000 characters.
    /// </summary>
    public static Complaint Raise(
        ComplaintOrderFacts order,
        Guid? orderItemId,
        ComplaintKind kind,
        string? description,
        int windowHours,
        bool hasOpenComplaint,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(order);
        var text = description?.Trim() ?? string.Empty;
        var errors = new List<FieldError>();
        if (text.Length is < MinDescriptionLength or > MaxDescriptionLength)
        {
            errors.Add(new FieldError("description", OrderingErrors.ValidationFailed, $"Describe the problem in {MinDescriptionLength}–{MaxDescriptionLength} characters."));
        }

        if (kind != ComplaintKind.Late && orderItemId is null)
        {
            errors.Add(new FieldError("orderItemId", OrderingErrors.ValidationFailed, "Choose the item the complaint is about."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        if (now > order.DeliveredAt.AddHours(windowHours))
        {
            throw new DomainException(OrderingErrors.ComplaintWindowClosed, $"Complaints can be raised within {windowHours} hours of delivery.");
        }

        if (hasOpenComplaint)
        {
            throw new ConflictException(OrderingErrors.ComplaintAlreadyOpen, "A complaint about this item is already open.");
        }

        var complaint = new Complaint(order, kind == ComplaintKind.Late ? null : orderItemId, kind, text, now);
        complaint.Raise(new ComplaintRaised(complaint.Id, order.OrderId, order.OrderNumber, order.CustomerId, order.FulfillmentLocationId, complaint.OrderItemId, kind.ToCode(), now));
        return complaint;
    }

    /// <summary>
    /// Decide an OPEN complaint (ORD-R57): APPROVED with REFUND (1 ≤ amount ≤ <paramref name="maxRefundPaise"/>) or
    /// REPLACEMENT (recorded only); REJECTED → resolution NONE. Raises ComplaintDecided.
    /// </summary>
    public void Decide(ComplaintStatus status, ComplaintResolution resolution, long? refundAmountPaise, long maxRefundPaise, string? note, Guid? decidedBy, DateTimeOffset now)
    {
        if (Status != ComplaintStatus.Open)
        {
            throw new ConflictException(OrderingErrors.InvalidStatusTransition, "Only an open complaint can be decided.");
        }

        switch (status)
        {
            case ComplaintStatus.Approved when resolution == ComplaintResolution.Refund:
                if (refundAmountPaise is not { } amount || amount < 1 || amount > maxRefundPaise)
                {
                    throw new DomainException(OrderingErrors.RefundAmountInvalid, $"The refund must be between 1 and {maxRefundPaise} paise.", "refundAmountPaise");
                }

                RefundAmountPaise = amount;
                break;
            case ComplaintStatus.Approved when resolution == ComplaintResolution.Replacement:
                RefundAmountPaise = null;
                break;
            case ComplaintStatus.Rejected:
                resolution = ComplaintResolution.None;
                RefundAmountPaise = null;
                break;
            default:
                throw new DomainException(OrderingErrors.ValidationFailed, "Approve with REFUND or REPLACEMENT, or reject.", "resolution");
        }

        Status = status;
        Resolution = resolution;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        DecidedBy = decidedBy;
        DecidedAt = now;
        Raise(new ComplaintDecided(Id, OrderId, OrderNumber, CustomerId, FulfillmentLocationId, OrderItemId, status.ToCode(), resolution.ToCode(), RefundAmountPaise, null, decidedBy, now));
    }

    /// <summary>APPROVED → RESOLVED.</summary>
    public void Resolve(Guid? resolvedBy, DateTimeOffset now)
    {
        if (Status != ComplaintStatus.Approved)
        {
            throw new ConflictException(OrderingErrors.InvalidStatusTransition, "Only an approved complaint can be resolved.");
        }

        Status = ComplaintStatus.Resolved;
        ResolvedBy = resolvedBy;
        ResolvedAt = now;
    }
}
