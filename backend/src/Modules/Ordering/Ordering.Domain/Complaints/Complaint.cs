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

    /// <summary>W12: most photos one complaint may carry.</summary>
    public const int MaxPhotos = 4;

    private readonly List<ComplaintPhoto> _photos = [];

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

    /// <summary>Photos the customer attached (W12), in upload order.</summary>
    public IReadOnlyList<ComplaintPhoto> Photos => _photos;

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

    /// <summary>
    /// W12: attaches a photo the customer uploaded (already re-encoded and stored under <paramref name="storageKey"/>).
    /// Only the complaint's customer, only while the complaint is OPEN (staff decide on what was there), at most
    /// <see cref="MaxPhotos"/>.
    /// </summary>
    public ComplaintPhoto AddPhoto(Guid photoId, Guid customerId, string storageKey, int width, int height, int sizeBytes, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        EnsureCanAddPhoto(customerId);
        var photo = new ComplaintPhoto(photoId, Id, storageKey, width, height, sizeBytes, now);
        _photos.Add(photo);
        return photo;
    }

    /// <summary>The <see cref="AddPhoto"/> checks alone, so a refused upload is refused before the image is processed.</summary>
    public void EnsureCanAddPhoto(Guid customerId)
    {
        if (customerId != CustomerId)
        {
            throw new NotFoundException("The complaint does not exist.");
        }

        if (Status != ComplaintStatus.Open)
        {
            throw new ConflictException(OrderingErrors.InvalidStatusTransition, "Photos can be added only while the complaint is open.");
        }

        if (_photos.Count >= MaxPhotos)
        {
            throw new DomainException(OrderingErrors.ComplaintPhotoLimitReached, $"A complaint can have at most {MaxPhotos} photos.", "file");
        }
    }

    /// <summary>Storage key of a complaint photo. No file extension, so the public <c>/media</c> route never serves it (SEC-20).</summary>
    public static string PhotoKey(Guid complaintId, Guid photoId) => $"complaints/{complaintId:D}/{photoId:D}";

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

/// <summary>
/// ORD-R57 refund bound of a complaint, shown to staff before deciding (G15) and enforced by
/// <see cref="Complaint.Decide"/>: a line complaint up to the line's final total less its final discount; an order-level
/// (LATE) complaint, or one whose line no longer exists, up to the order's final total (estimated total before packing).
/// Never below 0. Payments still caps the refund by what the payment can refund.
/// </summary>
public static class ComplaintRefundLimit
{
    public static long ForLine(long? lineFinalTotalPaise, long? lineFinalDiscountPaise)
        => Math.Max(0, (lineFinalTotalPaise ?? 0) - (lineFinalDiscountPaise ?? 0));

    public static long ForOrder(long? orderFinalTotalPaise, long orderTotalPaise)
        => Math.Max(0, orderFinalTotalPaise ?? orderTotalPaise);
}

/// <summary>
/// A photo attached to a complaint (<c>ordering.complaint_photos</c>, W12). The file is the re-encoded WebP (metadata
/// stripped, no original kept); it is private and served only through the complaint's customer and staff endpoints.
/// </summary>
public sealed class ComplaintPhoto : Entity
{
    public const string ContentType = "image/webp";

    private ComplaintPhoto()
    {
        StorageKey = string.Empty;
    }

    internal ComplaintPhoto(Guid id, Guid complaintId, string storageKey, int width, int height, int sizeBytes, DateTimeOffset uploadedAt)
        : base(id)
    {
        ComplaintId = complaintId;
        StorageKey = storageKey;
        Width = width;
        Height = height;
        SizeBytes = sizeBytes;
        UploadedAt = uploadedAt;
    }

    public Guid ComplaintId { get; private set; }

    public string StorageKey { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int SizeBytes { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }
}
