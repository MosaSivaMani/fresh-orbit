using System.Globalization;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.Ordering.Application.Orders;
using FreshOrbit.Ordering.Application.Shared;
using FreshOrbit.Ordering.Domain.Complaints;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Storage;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Ordering.Application.Complaints;

/// <summary>Body of <c>POST /orders/{id}/complaints</c> (JSON); photos are added afterwards, one per request (W12).</summary>
public sealed record RaiseComplaintRequest(Guid? OrderItemId, string? Kind, string? Description);

public sealed record DecideComplaintRequest(string? Status, string? Resolution, long? RefundAmountPaise, string? Note);

/// <summary>A complaint photo (W12). <c>Url</c> is an authenticated API path for the caller's audience (customer or staff).</summary>
public sealed record ComplaintPhotoDto(Guid Id, string Url, int Width, int Height, DateTimeOffset UploadedAt);

/// <param name="AttachmentUrls">The photo URLs (same as <c>Photos[].Url</c>), kept for clients of the first shape.</param>
/// <param name="ProductName">The complained-about line's product (G15); null for an order-level (LATE) complaint.</param>
/// <param name="MaxRefundPaise">Server-computed refund bound for this complaint (G15, ORD-R57); see <c>ComplaintRefundLimit</c>.</param>
public sealed record ComplaintResponse(
    Guid Id,
    string Version,
    Guid OrderId,
    string OrderNumber,
    Guid? OrderItemId,
    Guid FulfillmentLocationId,
    string Kind,
    string Description,
    string Status,
    string? Resolution,
    long? RefundAmountPaise,
    string? DecisionNote,
    DateTimeOffset RaisedAt,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<string> AttachmentUrls,
    string? ProductName,
    string? VariantName,
    string? PreparationName,
    long MaxRefundPaise,
    IReadOnlyList<ComplaintPhotoDto> Photos)
{
    public static ComplaintResponse From(Complaint complaint, ComplaintFacts facts, ComplaintAudience audience)
    {
        ArgumentNullException.ThrowIfNull(complaint);
        ArgumentNullException.ThrowIfNull(facts);
        var photos = complaint.Photos
            .OrderBy(p => p.UploadedAt).ThenBy(p => p.Id)
            .Select(p => new ComplaintPhotoDto(p.Id, ComplaintPhotoUrls.For(audience, complaint, p.Id), p.Width, p.Height, p.UploadedAt))
            .ToList();
        return new ComplaintResponse(
            complaint.Id,
            complaint.Version.ToString(CultureInfo.InvariantCulture),
            complaint.OrderId,
            complaint.OrderNumber,
            complaint.OrderItemId,
            complaint.FulfillmentLocationId,
            complaint.Kind.ToCode(),
            complaint.Description,
            complaint.Status.ToCode(),
            complaint.Resolution?.ToCode(),
            complaint.RefundAmountPaise,
            complaint.DecisionNote,
            complaint.RaisedAt,
            complaint.DecidedAt,
            complaint.ResolvedAt,
            [.. photos.Select(p => p.Url)],
            facts.ProductName,
            facts.VariantName,
            facts.PreparationName,
            facts.MaxRefundPaise,
            photos);
    }
}

/// <summary>Who reads a complaint: decides which photo URL it gets.</summary>
public enum ComplaintAudience
{
    Customer,
    Staff,
}

/// <summary>Order facts shown with a complaint (G15): the line's names and the refund bound.</summary>
public sealed record ComplaintFacts(string? ProductName, string? VariantName, string? PreparationName, long MaxRefundPaise)
{
    /// <summary>The facts of <paramref name="orderItemId"/> on <paramref name="order"/> (order level when null or gone).</summary>
    public static ComplaintFacts Of(Order order, Guid? orderItemId)
    {
        ArgumentNullException.ThrowIfNull(order);
        return orderItemId is { } id && order.FindItem(id) is { } item
            ? new ComplaintFacts(item.ProductName, item.VariantName, item.PreparationName, ComplaintRefundLimit.ForLine(item.FinalTotalPaise, item.FinalDiscountPaise))
            : new ComplaintFacts(null, null, null, ComplaintRefundLimit.ForOrder(order.FinalTotalPaise, order.TotalPaise));
    }
}

/// <summary>Authenticated API paths of complaint photos (W12); the files are never public.</summary>
public static class ComplaintPhotoUrls
{
    public static string For(ComplaintAudience audience, Complaint complaint, Guid photoId)
    {
        ArgumentNullException.ThrowIfNull(complaint);
        return audience == ComplaintAudience.Staff
            ? $"/api/v1/admin/complaints/{complaint.Id:D}/photos/{photoId:D}"
            : $"/api/v1/orders/{complaint.OrderId:D}/complaints/{complaint.Id:D}/photos/{photoId:D}";
    }
}

/// <summary>A complaint photo's bytes for download.</summary>
public sealed record ComplaintPhotoFile(Stream Content, string ContentType);

/// <summary>W12 upload limits.</summary>
public static class ComplaintPhotoRules
{
    /// <summary>Shortest side a photo needs (phone photos and screenshots pass; thumbnails do not).</summary>
    public const int MinShortestSide = 320;

    /// <summary>Width the stored WebP is scaled down to (never up).</summary>
    public const int MaxWidth = 1280;

    /// <summary>Largest upload accepted (the image limit, 5 MB).</summary>
    public const int MaxBytes = ImageLimits.MaxBytes;

    /// <summary>Request body limit of the upload route: the file plus multipart framing.</summary>
    public const long MaxRequestBytes = MaxBytes + (64 * 1024);
}

/// <summary>Complaints (ORD-R54..R57).</summary>
public sealed class ComplaintService(
    IComplaintRepository complaints,
    IOrderRepository orders,
    IOrderReads reads,
    IUnitOfWork unitOfWork,
    OrderQueryService queries,
    ISettingsApi settings,
    IImageSanitizer images,
    IFileStorage storage,
    IClock clock)
{
    public async Task<ComplaintResponse> RaiseAsync(Guid customerId, Guid orderId, RaiseComplaintRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var kind = EnumCodes.TryToEnum<ComplaintKind>(request.Kind, out var parsed)
            ? parsed
            : throw new RefusalException(new FieldError("kind", OrderingErrors.ValidationFailed, $"Choose one of {string.Join(", ", EnumCodes.AllCodes<ComplaintKind>())}."));
        var order = await queries.GetOwnedAsync(customerId, orderId, cancellationToken);
        if (order.Status != OrderStatus.Delivered || order.DeliveredAt is not { } deliveredAt)
        {
            throw new ConflictException(OrderingErrors.InvalidStatusTransition, "Complaints can be raised only for delivered orders.");
        }

        Guid? itemId = kind == ComplaintKind.Late ? null : request.OrderItemId;
        if (itemId is { } id && order.FindItem(id) is null)
        {
            throw new RefusalException(new FieldError("orderItemId", OrderingErrors.ValidationFailed, "The item does not belong to this order."));
        }

        var hasOpen = await complaints.HasOpenAsync(order.Id, itemId, cancellationToken);
        var policy = await settings.GetOrderPolicyAsync(order.CityId, cancellationToken);
        var complaint = Complaint.Raise(
            new ComplaintOrderFacts(order.Id, order.OrderNumber, order.CustomerId, order.FulfillmentLocationId, deliveredAt),
            itemId,
            kind,
            request.Description,
            policy.ComplaintWindowHours,
            hasOpen,
            clock.UtcNow);
        complaints.Add(complaint);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ComplaintResponse.From(complaint, ComplaintFacts.Of(order, complaint.OrderItemId), ComplaintAudience.Customer);
    }

    /// <summary>
    /// W12: adds one photo to the caller's OPEN complaint on their order (404 for anyone else's). The upload is read up
    /// to <see cref="ComplaintPhotoRules.MaxBytes"/>, checked and re-encoded by Catalog's image pipeline (SEC-5 limits,
    /// metadata dropped), stored under a private key, then committed; the file is deleted if the commit fails.
    /// </summary>
    public async Task<ComplaintResponse> AddPhotoAsync(Guid customerId, Guid orderId, Guid complaintId, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var order = await queries.GetOwnedAsync(customerId, orderId, cancellationToken);
        var complaint = await complaints.GetAsync(complaintId, cancellationToken);
        if (complaint is null || complaint.OrderId != order.Id)
        {
            throw new NotFoundException("The complaint does not exist.");
        }

        complaint.EnsureCanAddPhoto(customerId);
        var bytes = await ReadLimitedAsync(content, cancellationToken);
        var image = images.ToWebP(bytes, ComplaintPhotoRules.MinShortestSide, ComplaintPhotoRules.MaxWidth);
        var photoId = IdGenerator.NewId();
        var key = Complaint.PhotoKey(complaint.Id, photoId);
        try
        {
            using (var webp = new MemoryStream(image.WebP, writable: false))
            {
                await storage.SaveAsync(key, webp, SanitizedImage.ContentType, cancellationToken);
            }

            complaint.AddPhoto(photoId, customerId, key, image.Width, image.Height, image.WebP.Length, clock.UtcNow);
            complaints.MarkChanged(complaint);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        return ComplaintResponse.From(complaint, ComplaintFacts.Of(order, complaint.OrderItemId), ComplaintAudience.Customer);
    }

    /// <summary>A photo of the caller's complaint on their order, or 404.</summary>
    public async Task<ComplaintPhotoFile> OpenPhotoForCustomerAsync(Guid customerId, Guid orderId, Guid complaintId, Guid photoId, CancellationToken cancellationToken)
    {
        var complaint = await complaints.GetAsync(complaintId, cancellationToken);
        if (complaint is null || complaint.OrderId != orderId || complaint.CustomerId != customerId)
        {
            throw new NotFoundException("The complaint does not exist.");
        }

        return await OpenPhotoAsync(complaint, photoId, cancellationToken);
    }

    /// <summary>A photo of a complaint at one of the staff member's locations (else 404 / 403).</summary>
    public async Task<ComplaintPhotoFile> OpenPhotoForStaffAsync(ICurrentActor actor, Guid complaintId, Guid photoId, CancellationToken cancellationToken)
        => await OpenPhotoAsync(await GetForStaffAsync(actor, complaintId, cancellationToken), photoId, cancellationToken);

    /// <summary>Staff detail of a complaint with its line facts (G15) and photo URLs (W12).</summary>
    public async Task<ComplaintResponse> GetStaffViewAsync(ICurrentActor actor, Guid complaintId, CancellationToken cancellationToken)
        => await StaffViewAsync(await GetForStaffAsync(actor, complaintId, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<ComplaintResponse>> ListForOrderAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await queries.GetOwnedAsync(customerId, orderId, cancellationToken);
        return await reads.ListComplaintsForOrderAsync(order.Id, cancellationToken);
    }

    public Task<(IReadOnlyList<ComplaintResponse> Items, int Total)> AdminListAsync(ICurrentActor actor, string? status, string? kind, Guid? fulfillmentLocationId, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (fulfillmentLocationId is { } locationId && !actor.CanAccessLocation(locationId))
        {
            throw OrderingAccess.LocationDenied();
        }

        return reads.ListComplaintsAsync(
            OrderQueryService.ParseOptional<ComplaintStatus>(status, "status"),
            OrderQueryService.ParseOptional<ComplaintKind>(kind, "kind"),
            fulfillmentLocationId,
            actor.IsAllLocations ? null : actor.LocationIds,
            page,
            pageSize,
            cancellationToken);
    }

    public async Task<Complaint> GetForStaffAsync(ICurrentActor actor, Guid complaintId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var complaint = await complaints.GetAsync(complaintId, cancellationToken) ?? throw new NotFoundException("The complaint does not exist.");
        return actor.CanAccessLocation(complaint.FulfillmentLocationId) ? complaint : throw OrderingAccess.LocationDenied();
    }

    /// <summary>ORD-R57: decide an OPEN complaint; refund bounded by the line's (or order's, LATE) final total.</summary>
    public async Task<ComplaintResponse> DecideAsync(ICurrentActor actor, Guid complaintId, uint expectedVersion, DecideComplaintRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<FieldError>();
        if (!EnumCodes.TryToEnum<ComplaintStatus>(request.Status, out var status) || status is not (ComplaintStatus.Approved or ComplaintStatus.Rejected))
        {
            errors.Add(new FieldError("status", OrderingErrors.ValidationFailed, "Choose APPROVED or REJECTED."));
        }

        var resolution = ComplaintResolution.None;
        if (request.Resolution is not null && !EnumCodes.TryToEnum(request.Resolution, out resolution))
        {
            errors.Add(new FieldError("resolution", OrderingErrors.ValidationFailed, "Choose REFUND, REPLACEMENT or NONE."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        var complaint = await GetForStaffAsync(actor, complaintId, cancellationToken);
        unitOfWork.SetExpectedVersion(complaint, expectedVersion);
        var order = await orders.GetAsync(complaint.OrderId, cancellationToken) ?? throw new NotFoundException("The order does not exist.");
        var facts = ComplaintFacts.Of(order, complaint.OrderItemId);
        complaint.Decide(status, resolution, request.RefundAmountPaise, facts.MaxRefundPaise, request.Note, actor.StaffMemberId ?? actor.UserId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ComplaintResponse.From(complaint, facts, ComplaintAudience.Staff);
    }

    public async Task<ComplaintResponse> ResolveAsync(ICurrentActor actor, Guid complaintId, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var complaint = await GetForStaffAsync(actor, complaintId, cancellationToken);
        unitOfWork.SetExpectedVersion(complaint, expectedVersion);
        complaint.Resolve(actor.StaffMemberId ?? actor.UserId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await StaffViewAsync(complaint, cancellationToken);
    }

    private async Task<ComplaintResponse> StaffViewAsync(Complaint complaint, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(complaint.OrderId, cancellationToken) ?? throw new NotFoundException("The order does not exist.");
        return ComplaintResponse.From(complaint, ComplaintFacts.Of(order, complaint.OrderItemId), ComplaintAudience.Staff);
    }

    private async Task<ComplaintPhotoFile> OpenPhotoAsync(Complaint complaint, Guid photoId, CancellationToken cancellationToken)
    {
        var photo = complaint.Photos.FirstOrDefault(p => p.Id == photoId) ?? throw new NotFoundException("The photo does not exist.");
        var stream = await storage.OpenReadAsync(photo.StorageKey, cancellationToken) ?? throw new NotFoundException("The photo does not exist.");
        return new ComplaintPhotoFile(stream, ComplaintPhoto.ContentType);
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > ComplaintPhotoRules.MaxBytes)
            {
                throw new RefusalException(new FieldError("file", OrderingErrors.ImageInvalid, $"Upload a photo of at most {ComplaintPhotoRules.MaxBytes / (1024 * 1024)} MB."));
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
