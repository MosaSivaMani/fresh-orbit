using FreshOrbit.Delivery.Public;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.Ordering.Application.Invoices;
using FreshOrbit.Ordering.Application.Shared;
using FreshOrbit.Ordering.Domain.Invoices;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.Payments.Public;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Ordering.Application.Orders;

/// <summary>Order reads for customers and staff (ORD-R21..R24, R60).</summary>
public sealed class OrderQueryService(
    IOrderRepository orders,
    IInvoiceRepository invoices,
    IOrderReads reads,
    IPaymentsApi payments,
    ISettingsApi settings,
    IShipmentTracking shipments,
    ILocationsApi locations,
    IInventoryApi inventory,
    IDeliveryApi delivery,
    IClock clock)
{
    /// <summary>Parses the policy's <c>cancelAllowedUntilStatus</c>; CONFIRMED when missing or unknown.</summary>
    public static OrderStatus CancelLimit(OrderPolicy policy)
        => policy is not null && EnumCodes.TryToEnum<OrderStatus>(policy.CancelAllowedUntilStatus, out var status) ? status : OrderStatus.Confirmed;

    /// <summary>ORD-R38 as a yes/no for <c>allowedActions.cancel</c>.</summary>
    public static bool CustomerMayCancel(Order order, OrderStatus cancelAllowedUntil)
    {
        ArgumentNullException.ThrowIfNull(order);
        return order.Status == OrderStatus.PendingPayment
            || (order.Status is OrderStatus.Confirmed or OrderStatus.Preparing && Order.Rank(order.Status) <= Order.Rank(cancelAllowedUntil));
    }

    /// <summary>The customer's order or 404 (ORD-R22).</summary>
    public async Task<Order> GetOwnedAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(orderId, cancellationToken);
        return order is not null && order.CustomerId == customerId ? order : throw new NotFoundException("The order does not exist.");
    }

    /// <summary>The order when the staff member may act on its location (ORD-R21), else 404 / 403.</summary>
    public async Task<Order> GetForStaffAsync(ICurrentActor actor, Guid orderId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var order = await orders.GetAsync(orderId, cancellationToken) ?? throw new NotFoundException("The order does not exist.");
        return actor.CanAccessLocation(order.FulfillmentLocationId) ? order : throw OrderingAccess.LocationDenied();
    }

    public async Task<OrderDetailResponse> CustomerDetailAsync(Order order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        var payment = await payments.GetPaymentStatusAsync(order.Id, cancellationToken);
        var invoice = await invoices.FindByOrderAsync(order.Id, cancellationToken);
        var policy = await settings.GetOrderPolicyAsync(order.CityId, cancellationToken);
        var now = clock.UtcNow;
        var actions = new AllowedActionsDto(
            CustomerMayCancel(order, CancelLimit(policy)),
            order.Status == OrderStatus.Delivered && order.DeliveredAt is { } delivered && now <= delivered.AddHours(policy.ComplaintWindowHours),
            invoice is not null,
            true);
        return OrderViews.Detail(order, new OrderPaymentView(payment.Status.ToCode(), payment.PaidPaise, payment.RefundedPaise), invoice, actions);
    }

    public async Task<OrderDetailResponse> GetCustomerOrderAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
        => await CustomerDetailAsync(await GetOwnedAsync(customerId, orderId, cancellationToken), cancellationToken);

    public Task<(IReadOnlyList<OrderListItem> Items, int Total)> ListCustomerOrdersAsync(Guid customerId, string? status, int page, int pageSize, CancellationToken cancellationToken)
        => reads.ListForCustomerAsync(customerId, ParseOptional<OrderStatus>(status, "status"), page, pageSize, cancellationToken);

    public async Task<OrderTrackingResponse> TrackingAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await GetOwnedAsync(customerId, orderId, cancellationToken);
        var shipment = await shipments.GetShipmentForOrderAsync(order.Id, cancellationToken);
        return OrderViews.Tracking(order, shipment is null
            ? null
            : new ShipmentTrackingView(
                shipment.Status,
                shipment.RiderName,
                shipment.RiderPhone,
                shipment.DispatchedAt,
                shipment.ColdBoxUsed,
                shipment.DeliveryCode,
                [.. shipment.Attempts.Select(a => new TrackingAttemptDto(a.AttemptNo, a.Outcome, a.AttemptedAt))]));
    }

    public async Task<AdminOrderDetailResponse> AdminDetailAsync(ICurrentActor actor, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await GetForStaffAsync(actor, orderId, cancellationToken);
        var detail = await CustomerDetailAsync(order, cancellationToken);
        var stock = await inventory.GetOrderStockAsync(order.Id, cancellationToken);
        var view = new OrderStockView(
            stock.Reservations.Select(r => (r.OrderItemId, r.Status)),
            stock.Sales.Select(s => new OrderStockBatch(s.MovementId, s.OrderItemId, s.BatchId, s.BatchCode, s.Quantity, s.OccurredAt)));
        return OrderViews.AdminDetail(order, detail, view);
    }

    public async Task<(IReadOnlyList<AdminOrderListItem> Items, int Total)> AdminListAsync(ICurrentActor actor, AdminOrderListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(query);
        var statuses = ParseList<OrderStatus>(query.Statuses, "status");
        var method = ParseOptional<PaymentMethod>(query.PaymentMethod, "paymentMethod");
        if (query.FulfillmentLocationId is { } locationId && !actor.CanAccessLocation(locationId))
        {
            throw OrderingAccess.LocationDenied();
        }

        DateTimeOffset? slotFrom = null;
        DateTimeOffset? slotUntil = null;
        if (query.SlotDate is { } slotDate)
        {
            // The slot date is a local date of the location (its time zone); without a location it is a UTC date.
            var zone = TimeZoneInfo.Utc;
            if (query.FulfillmentLocationId is { } id && (await locations.GetFulfillmentLocationsAsync([id], cancellationToken)) is [var location, ..])
            {
                zone = TimeZones.Find(location.TimeZone);
            }

            var start = slotDate.ToDateTime(TimeOnly.MinValue);
            slotFrom = new DateTimeOffset(start, zone.GetUtcOffset(start));
            slotUntil = slotFrom.Value.AddDays(1);
        }

        var filter = new AdminOrderFilter(query.FulfillmentLocationId, statuses, slotFrom, slotUntil, query.SlotId, method, query.Q, actor.IsAllLocations ? null : actor.LocationIds, query.Page, query.PageSize);
        return await reads.ListForStaffAsync(filter, cancellationToken);
    }

    public async Task<PickListResponse> PickListAsync(ICurrentActor actor, Guid fulfillmentLocationId, Guid slotId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.CanAccessLocation(fulfillmentLocationId))
        {
            throw OrderingAccess.LocationDenied();
        }

        var data = await reads.PickListAsync(fulfillmentLocationId, slotId, cancellationToken);
        var slot = await delivery.GetSlotAsync(slotId, fulfillmentLocationId, cancellationToken);
        return new PickListResponse(
            fulfillmentLocationId,
            slotId,
            data.Lines,
            slot is null ? null : new PickListSlotDto(slot.SlotDate, slot.StartAt, slot.EndAt, slot.CutoffAt),
            data.OrderCount);
    }

    public async Task<InvoiceResponse> CustomerInvoiceAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
        => await InvoiceOfAsync(await GetOwnedAsync(customerId, orderId, cancellationToken), InvoicePdfService.CustomerUrl, cancellationToken);

    public async Task<InvoiceResponse> StaffInvoiceAsync(ICurrentActor actor, Guid orderId, CancellationToken cancellationToken)
        => await InvoiceOfAsync(await GetForStaffAsync(actor, orderId, cancellationToken), InvoicePdfService.StaffUrl, cancellationToken);

    /// <summary>
    /// The staff invoice register (D353, D354): orders.view, location-scoped like the order list (a named location the
    /// staff member cannot see → 403; otherwise rows silently limited to their locations). Dates are invoice dates in the
    /// business time zone, the zone printed on the PDF; an inverted range is refused on <c>toDate</c>.
    /// </summary>
    public async Task<(IReadOnlyList<AdminInvoiceListItem> Items, int Total)> AdminInvoicesAsync(ICurrentActor actor, AdminInvoiceListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(query);
        if (query.FulfillmentLocationId is { } locationId && !actor.CanAccessLocation(locationId))
        {
            throw OrderingAccess.LocationDenied();
        }

        if (query is { FromDate: { } fromDate, ToDate: { } toDate } && fromDate > toDate)
        {
            throw new RefusalException(new FieldError("toDate", OrderingErrors.ValidationFailed, "To date must not be before from date."));
        }

        var zone = InvoicePdfService.BusinessTimeZone(await settings.GetBusinessProfileAsync(cancellationToken));
        var filter = new AdminInvoiceFilter(
            query.FulfillmentLocationId,
            query.FromDate is { } from ? StartOfDay(from, zone) : null,
            query.ToDate is { } to ? StartOfDay(to.AddDays(1), zone) : null,
            query.Q,
            actor.IsAllLocations ? null : actor.LocationIds,
            query.Page,
            query.PageSize);
        var (rows, total) = await reads.ListInvoicesAsync(filter, cancellationToken);
        return ([.. rows.Select(r => new AdminInvoiceListItem(
            r.Id,
            r.InvoiceNumber,
            r.IssuedAt,
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(r.IssuedAt, zone).DateTime),
            r.OrderId,
            r.OrderNumber,
            r.CustomerName,
            r.FulfillmentLocationId,
            r.TaxablePaise,
            r.TaxPaise,
            r.TotalPaise,
            r.PdfReady,
            r.PdfReady ? InvoicePdfService.StaffPdfPath(r.OrderId) : null))], total);
    }

    /// <summary>Midnight starting <paramref name="date"/> in <paramref name="zone"/>, as a UTC instant.</summary>
    private static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo zone)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(start, zone.GetUtcOffset(start)).ToUniversalTime();
    }

    internal static TEnum? ParseOptional<TEnum>(string? code, string field)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return EnumCodes.TryToEnum<TEnum>(code.Trim(), out var value)
            ? value
            : throw new RefusalException(new FieldError(field, OrderingErrors.ValidationFailed, $"Unknown {field} '{code}'."));
    }

    /// <summary>
    /// Codes from repeated query values, each of which may also be a comma list (<c>status=CONFIRMED&amp;status=PREPARING</c>
    /// or <c>status=CONFIRMED,PREPARING</c>); blanks ignored, duplicates removed, an unknown code is refused (G14).
    /// </summary>
    internal static IReadOnlyCollection<TEnum> ParseList<TEnum>(IEnumerable<string?>? values, string field)
        where TEnum : struct, Enum
    {
        var parsed = new HashSet<TEnum>();
        foreach (var value in values ?? [])
        {
            foreach (var code in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                parsed.Add(ParseOptional<TEnum>(code, field)!.Value);
            }
        }

        return parsed;
    }

    private async Task<InvoiceResponse> InvoiceOfAsync(Order order, Func<Invoice, string?> pdfUrl, CancellationToken cancellationToken)
    {
        var invoice = await invoices.FindByOrderAsync(order.Id, cancellationToken)
            ?? throw new NotFoundException(OrderingErrors.InvoiceNotIssued, "The invoice is issued when the order is delivered.");
        return OrderViews.Invoice(order, invoice, pdfUrl(invoice));
    }
}
