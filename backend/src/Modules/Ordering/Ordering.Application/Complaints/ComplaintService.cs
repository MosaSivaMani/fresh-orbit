using System.Globalization;
using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.Ordering.Application.Orders;
using FreshOrbit.Ordering.Application.Shared;
using FreshOrbit.Ordering.Domain.Complaints;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Ordering.Application.Complaints;

/// <summary>Body of <c>POST /orders/{id}/complaints</c> (JSON; images deferred until file storage lands, D21).</summary>
public sealed record RaiseComplaintRequest(Guid? OrderItemId, string? Kind, string? Description);

public sealed record DecideComplaintRequest(string? Status, string? Resolution, long? RefundAmountPaise, string? Note);

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
    IReadOnlyList<string> AttachmentUrls)
{
    public static ComplaintResponse From(Complaint complaint)
    {
        ArgumentNullException.ThrowIfNull(complaint);
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
            []);
    }
}

/// <summary>Complaints (ORD-R54..R57).</summary>
public sealed class ComplaintService(
    IComplaintRepository complaints,
    IOrderRepository orders,
    IOrderReads reads,
    IUnitOfWork unitOfWork,
    OrderQueryService queries,
    OrderingOptions options,
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
        var complaint = Complaint.Raise(
            new ComplaintOrderFacts(order.Id, order.OrderNumber, order.CustomerId, order.FulfillmentLocationId, deliveredAt),
            itemId,
            kind,
            request.Description,
            options.ComplaintWindowHours,
            hasOpen,
            clock.UtcNow);
        complaints.Add(complaint);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ComplaintResponse.From(complaint);
    }

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
        var maxRefund = complaint.OrderItemId is { } itemId && order.FindItem(itemId) is { } item
            ? (item.FinalTotalPaise ?? 0) - (item.FinalDiscountPaise ?? 0)
            : order.FinalTotalPaise ?? order.TotalPaise;
        complaint.Decide(status, resolution, request.RefundAmountPaise, maxRefund, request.Note, actor.StaffMemberId ?? actor.UserId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ComplaintResponse.From(complaint);
    }

    public async Task<ComplaintResponse> ResolveAsync(ICurrentActor actor, Guid complaintId, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var complaint = await GetForStaffAsync(actor, complaintId, cancellationToken);
        unitOfWork.SetExpectedVersion(complaint, expectedVersion);
        complaint.Resolve(actor.StaffMemberId ?? actor.UserId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ComplaintResponse.From(complaint);
    }
}
