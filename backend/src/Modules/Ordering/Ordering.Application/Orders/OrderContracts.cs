using System.Globalization;
using FreshOrbit.Ordering.Domain.Invoices;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.SharedKernel.Codes;

namespace FreshOrbit.Ordering.Application.Orders;

public sealed record QuantityDto(int Value, string Unit);

public sealed record SlotDto(DateTimeOffset StartAt, DateTimeOffset EndAt);

public sealed record OrderAddressDto(
    string RecipientName,
    string Phone,
    string Line1,
    string? Line2,
    string? Landmark,
    string Locality,
    string CityName,
    string StateCode,
    string Pincode);

public sealed record OrderItemDto(
    Guid Id,
    int LineNo,
    Guid VariantId,
    string ProductName,
    string VariantName,
    string? PreparationName,
    string? ImageKey,
    QuantityDto OrderedQuantity,
    QuantityDto MaxChargeableQuantity,
    QuantityDto? PackedQuantity,
    QuantityDto? ChargedQuantity,
    long UnitPricePaise,
    long PreparationChargePaise,
    long EstimatedTotalPaise,
    long? FinalTotalPaise,
    string Status,
    Guid? SubstitutedFromItemId);

public sealed record AmountsDto(long ItemsPaise, long PreparationPaise, long DiscountPaise, long DeliveryFeePaise, long TaxPaise, long TotalPaise);

public sealed record SubstitutionDto(
    Guid Id,
    Guid OrderItemId,
    string ProposedName,
    QuantityDto ProposedQuantity,
    long PriceDifferencePaise,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt,
    Guid? NewOrderItemId);

public sealed record OrderPaymentDto(string Method, string Status, object? NextAction);

public sealed record OrderInvoiceRefDto(Guid Id, string InvoiceNumber, DateTimeOffset IssuedAt);

public sealed record AllowedActionsDto(bool Cancel, bool RaiseComplaint, bool DownloadInvoice, bool Reorder);

/// <summary>Order detail (docs/api §4.8 GET /orders/{id}); <c>final</c> null until PACKED.</summary>
public sealed record OrderDetailResponse(
    Guid Id,
    string OrderNumber,
    string Version,
    string Status,
    string PaymentMethod,
    DateTimeOffset PlacedAt,
    SlotDto Slot,
    OrderAddressDto Address,
    string SubstitutionPreference,
    IReadOnlyList<OrderItemDto> Items,
    AmountsDto Estimate,
    AmountsDto? Final,
    long AmountDuePaise,
    long AmountPaidPaise,
    long AmountRefundedPaise,
    string? CouponCode,
    string? CustomerNotes,
    IReadOnlyList<SubstitutionDto> Substitutions,
    OrderPaymentDto Payment,
    OrderInvoiceRefDto? Invoice,
    AllowedActionsDto AllowedActions);

public sealed record OrderListItem(
    Guid Id,
    string OrderNumber,
    string Status,
    string PaymentMethod,
    DateTimeOffset PlacedAt,
    DateTimeOffset SlotStartAt,
    DateTimeOffset SlotEndAt,
    int ItemCount,
    long TotalPaise,
    long? FinalTotalPaise,
    long AmountDuePaise);

public sealed record AdminOrderListItem(
    Guid Id,
    string OrderNumber,
    string Status,
    string PaymentMethod,
    Guid CustomerId,
    string RecipientName,
    string Phone,
    Guid FulfillmentLocationId,
    Guid DeliverySlotId,
    DateTimeOffset PlacedAt,
    DateTimeOffset SlotStartAt,
    DateTimeOffset SlotEndAt,
    int ItemCount,
    long TotalPaise,
    long? FinalTotalPaise,
    long AmountDuePaise,
    string Version);

public sealed record OrderHistoryDto(string? From, string To, string ActorType, Guid? ActorId, string? ReasonCode, DateTimeOffset At);

public sealed record OrderAdjustmentDto(Guid Id, Guid? OrderItemId, string Kind, long AmountPaise, string Reason, Guid? CreatedBy, DateTimeOffset At);

/// <summary>
/// One batch a line's stock was taken from (G17): a SALE movement written when the order was packed, FEFO. Empty
/// before packing (reservations are not tied to a batch). <c>BatchId</c>/<c>BatchCode</c> are null for the batchless
/// pack-shortfall movement. <c>Quantity</c> is the gross quantity taken, in the stock unit.
/// </summary>
public sealed record OrderLineBatchDto(Guid MovementId, Guid? BatchId, string? BatchCode, QuantityDto Quantity, DateTimeOffset OccurredAt);

/// <param name="ReservationStatus">ACTIVE, CONSUMED, RELEASED or EXPIRED; null when the line has no reservation (G17).</param>
/// <param name="Batches">Batch allocations of the line (G17), oldest first.</param>
public sealed record OrderLineStockDto(Guid OrderItemId, Guid VariantId, string? PreparationCode, QuantityDto GrossQuantity, int YieldBp, string? ReservationStatus, IReadOnlyList<OrderLineBatchDto> Batches);

/// <summary>Staff detail: the customer view plus history, adjustments and gross quantities (§4.15).</summary>
public sealed record AdminOrderDetailResponse(
    OrderDetailResponse Order,
    Guid CustomerId,
    Guid CityId,
    Guid FulfillmentLocationId,
    Guid ServiceAreaId,
    Guid DeliverySlotId,
    string? CancelReasonCode,
    bool ColdBoxUsed,
    long RefundDuePaise,
    IReadOnlyList<OrderLineStockDto> Lines,
    IReadOnlyList<OrderHistoryDto> History,
    IReadOnlyList<OrderAdjustmentDto> Adjustments);

public sealed record TimelineEntryDto(string Status, DateTimeOffset At);

public sealed record TrackingShipmentDto(
    string Status,
    string? RiderName,
    string? RiderPhone,
    DateTimeOffset? DispatchedAt,
    bool ColdBoxUsed,
    IReadOnlyList<TrackingAttemptDto> Attempts);

public sealed record TrackingAttemptDto(int AttemptNo, string Outcome, DateTimeOffset AttemptedAt);

public sealed record OrderTrackingResponse(
    string OrderNumber,
    string Status,
    IReadOnlyList<TimelineEntryDto> Timeline,
    SlotDto Slot,
    TrackingShipmentDto? Shipment,
    string? DeliveryCode,
    long AmountDuePaise);

public sealed record PackedLineDto(Guid OrderItemId, QuantityDto Packed, QuantityDto Charged, bool Capped, long FinalTotalPaise, long AdjustmentPaise, string Status);

public sealed record PackResponse(Guid OrderId, string Status, string Version, IReadOnlyList<PackedLineDto> Lines, long FinalTotalPaise, long AmountDuePaise, long RefundDuePaise);

public sealed record PickListLine(Guid VariantId, string ProductName, string VariantName, string? PreparationCode, string? PreparationName, string StockUnit, long GrossQuantity, int OrderCount, string Sku);

/// <summary>The slot's local date and times (G18), from Delivery.</summary>
public sealed record PickListSlotDto(DateOnly SlotDate, DateTimeOffset StartAt, DateTimeOffset EndAt, DateTimeOffset CutoffAt);

/// <param name="Slot">Null when Delivery has no such slot at the location.</param>
/// <param name="OrderCount">Distinct CONFIRMED/PREPARING orders of the slot with at least one ACTIVE line (G18).</param>
public sealed record PickListResponse(Guid FulfillmentLocationId, Guid SlotId, IReadOnlyList<PickListLine> Lines, PickListSlotDto? Slot, int OrderCount);

/// <summary>Pick list rows plus the number of distinct orders they come from (one query each).</summary>
public sealed record PickListData(IReadOnlyList<PickListLine> Lines, int OrderCount);

public sealed record InvoiceLineDto(int LineNo, string Description, string HsnCode, QuantityDto Quantity, long UnitPricePaise, long TaxablePaise, int TaxRateBp, long TaxPaise, long CgstPaise, long SgstPaise, long TotalPaise);

/// <summary>Invoice JSON; <c>PdfUrl</c> stays null until PDF rendering exists (D20: may be deferred).</summary>
public sealed record InvoiceResponse(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string InvoiceNumber,
    DateTimeOffset IssuedAt,
    string SellerLegalName,
    string SellerGstin,
    IReadOnlyList<InvoiceLineDto> Lines,
    long TaxablePaise,
    long TaxPaise,
    long TotalPaise,
    string? PdfUrl);

// Requests

public sealed record CancelOrderRequest(string? ReasonCode, string? Note);

public sealed record PackLineRequest(Guid OrderItemId, QuantityDto? PackedQuantity);

public sealed record PackOrderRequest(IReadOnlyList<PackLineRequest>? Lines, bool ColdBoxUsed);

public sealed record ProposeSubstitutionRequest(Guid VariantId, Guid? PreparationMethodId, QuantityDto? Quantity);

public sealed record RecordSubstitutionResponseRequest(string? Answer);

public sealed record GoodwillRequest(long AmountPaise, string? Reason, Guid? OrderItemId);

/// <param name="Statuses">Status codes (G14): repeated <c>status=</c> values, each may also be a comma list; OR within the field.</param>
public sealed record AdminOrderListQuery(
    Guid? FulfillmentLocationId,
    IReadOnlyList<string> Statuses,
    DateOnly? SlotDate,
    Guid? SlotId,
    string? PaymentMethod,
    string? Q,
    int Page,
    int PageSize);

/// <summary>Typed staff list filter; <c>LocationIds</c> = locations the staff member may see (null = all).</summary>
public sealed record AdminOrderFilter(
    Guid? FulfillmentLocationId,
    IReadOnlyCollection<OrderStatus> Statuses,
    DateTimeOffset? SlotFrom,
    DateTimeOffset? SlotUntil,
    Guid? SlotId,
    PaymentMethod? PaymentMethod,
    string? Q,
    IReadOnlySet<Guid>? LocationIds,
    int Page,
    int PageSize);

/// <summary>Maps orders to responses.</summary>
public static class OrderViews
{
    public static string VersionOf(uint version) => version.ToString(CultureInfo.InvariantCulture);

    public static OrderDetailResponse Detail(Order order, OrderPaymentView payment, Invoice? invoice, AllowedActionsDto allowedActions)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(payment);
        return new OrderDetailResponse(
            order.Id,
            order.OrderNumber,
            VersionOf(order.Version),
            order.Status.ToCode(),
            order.PaymentMethod.ToCode(),
            order.PlacedAt,
            new SlotDto(order.SlotStartAt, order.SlotEndAt),
            new OrderAddressDto(order.RecipientName, order.Phone, order.Line1, order.Line2, order.Landmark, order.Locality, order.CityName, order.StateCode, order.Pincode),
            order.SubstitutionPreference.ToCode(),
            [.. order.Items.OrderBy(i => i.LineNo).Select(Item)],
            new AmountsDto(order.ItemsPaise, order.PreparationPaise, order.DiscountPaise, order.DeliveryFeePaise, order.TaxPaise, order.TotalPaise),
            order.FinalTotalPaise is { } finalTotal
                ? new AmountsDto(order.FinalItemsPaise ?? 0, order.FinalPreparationPaise ?? 0, order.FinalDiscountPaise ?? 0, order.DeliveryFeePaise, order.FinalTaxPaise ?? 0, finalTotal)
                : null,
            order.AmountDuePaise,
            payment.PaidPaise,
            payment.RefundedPaise,
            order.CouponCode,
            order.CustomerNotes,
            [.. order.Substitutions.OrderBy(s => s.ProposedAt).Select(Substitution)],
            new OrderPaymentDto(order.PaymentMethod.ToCode(), payment.Status, null),
            invoice is null ? null : new OrderInvoiceRefDto(invoice.Id, invoice.InvoiceNumber, invoice.IssuedAt),
            allowedActions);
    }

    public static AdminOrderDetailResponse AdminDetail(Order order, OrderDetailResponse detail, OrderStockView? stock = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        stock ??= OrderStockView.Empty;
        return new AdminOrderDetailResponse(
            detail,
            order.CustomerId,
            order.CityId,
            order.FulfillmentLocationId,
            order.ServiceAreaId,
            order.DeliverySlotId,
            order.CancelReasonCode,
            order.ColdBoxUsed,
            order.RefundDuePaise,
            [.. order.Items.OrderBy(i => i.LineNo).Select(i => new OrderLineStockDto(
                i.Id,
                i.VariantId,
                i.PreparationCode,
                new QuantityDto(i.GrossQuantity, i.StockUnit),
                i.YieldBp,
                stock.ReservationStatusOf(i.Id),
                stock.BatchesOf(i.Id, i.StockUnit)))],
            [.. order.History.OrderBy(h => h.At).Select(h => new OrderHistoryDto(h.FromStatus?.ToCode(), h.ToStatus.ToCode(), h.ActorType.ToCode(), h.ActorId, h.ReasonCode, h.At))],
            [.. order.Adjustments.OrderBy(a => a.At).Select(a => new OrderAdjustmentDto(a.Id, a.OrderItemId, a.Kind.ToCode(), a.AmountPaise, a.Reason, a.CreatedByActorId, a.At))]);
    }

    public static PackResponse Pack(Order order, IReadOnlyList<PackedLineResult> lines)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(lines);
        return new PackResponse(
            order.Id,
            order.Status.ToCode(),
            VersionOf(order.Version),
            [.. lines.Select(l => new PackedLineDto(l.OrderItemId, new QuantityDto(l.PackedQuantity, l.Unit), new QuantityDto(l.ChargedQuantity, l.Unit), l.Capped, l.FinalTotalPaise, l.AdjustmentPaise, l.Status.ToCode()))],
            order.FinalTotalPaise ?? 0,
            order.AmountDuePaise,
            order.RefundDuePaise);
    }

    public static InvoiceResponse Invoice(Order order, Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(invoice);
        return new InvoiceResponse(
            invoice.Id,
            order.Id,
            order.OrderNumber,
            invoice.InvoiceNumber,
            invoice.IssuedAt,
            invoice.SellerLegalName,
            invoice.SellerGstin,
            [.. invoice.Lines.OrderBy(l => l.LineNo).Select(l => new InvoiceLineDto(l.LineNo, l.Description, l.HsnCode, new QuantityDto(l.Quantity, l.Unit), l.UnitPricePaise, l.TaxablePaise, l.TaxRateBp, l.TaxPaise, l.CgstPaise, l.SgstPaise, l.TotalPaise))],
            invoice.TaxablePaise,
            invoice.TaxPaise,
            invoice.TotalPaise,
            null);
    }

    public static OrderTrackingResponse Tracking(Order order, ShipmentTrackingView? shipment)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new OrderTrackingResponse(
            order.OrderNumber,
            order.Status.ToCode(),
            [.. order.History.OrderBy(h => h.At).Select(h => new TimelineEntryDto(h.ToStatus.ToCode(), h.At))],
            new SlotDto(order.SlotStartAt, order.SlotEndAt),
            shipment is null ? null : new TrackingShipmentDto(shipment.Status, shipment.RiderName, shipment.RiderPhone, shipment.DispatchedAt, shipment.ColdBoxUsed, shipment.Attempts),
            order.Status == OrderStatus.OutForDelivery ? shipment?.DeliveryCode : null,
            order.AmountDuePaise);
    }

    private static OrderItemDto Item(OrderItem item)
    {
        var unit = item.QuantityUnit;
        return new OrderItemDto(
            item.Id,
            item.LineNo,
            item.VariantId,
            item.ProductName,
            item.VariantName,
            item.PreparationName,
            item.ImageKey,
            new QuantityDto(item.OrderedQuantity, unit),
            new QuantityDto(item.MaxChargeableQuantity, unit),
            item.PackedQuantity is { } packed ? new QuantityDto(packed, unit) : null,
            item.ChargedQuantity is { } charged ? new QuantityDto(charged, unit) : null,
            item.UnitPricePaise,
            item.FinalPreparationPaise ?? item.EstimatedPreparationPaise,
            item.EstimatedTotalPaise,
            item.FinalTotalPaise,
            item.Status.ToCode(),
            item.SubstitutedFromItemId);
    }

    private static SubstitutionDto Substitution(Substitution substitution) => new(
        substitution.Id,
        substitution.OrderItemId,
        substitution.ProposedName,
        new QuantityDto(substitution.ProposedQuantity, substitution.QuantityUnit),
        substitution.PriceDifferencePaise,
        substitution.Status.ToCode(),
        substitution.ExpiresAt,
        substitution.RespondedAt,
        substitution.NewOrderItemId);
}

/// <summary>Inventory facts of an order (Inventory.Public <c>GetOrderStock</c>), indexed by order item (G17).</summary>
public sealed class OrderStockView
{
    public static readonly OrderStockView Empty = new([], []);

    private readonly ILookup<Guid, OrderStockBatch> _batches;
    private readonly Dictionary<Guid, string> _reservations;

    public OrderStockView(IEnumerable<(Guid OrderItemId, string Status)> reservations, IEnumerable<OrderStockBatch> batches)
    {
        ArgumentNullException.ThrowIfNull(reservations);
        ArgumentNullException.ThrowIfNull(batches);

        // One reservation per line; a replaced one (substitution) keeps the newest status seen last.
        _reservations = [];
        foreach (var (itemId, status) in reservations)
        {
            _reservations[itemId] = status;
        }

        _batches = batches.ToLookup(b => b.OrderItemId);
    }

    public string? ReservationStatusOf(Guid orderItemId) => _reservations.GetValueOrDefault(orderItemId);

    public IReadOnlyList<OrderLineBatchDto> BatchesOf(Guid orderItemId, string stockUnit)
        => [.. _batches[orderItemId].OrderBy(b => b.OccurredAt).ThenBy(b => b.MovementId)
            .Select(b => new OrderLineBatchDto(b.MovementId, b.BatchId, b.BatchCode, new QuantityDto(Math.Abs(b.Quantity), stockUnit), b.OccurredAt))];
}

/// <summary>One SALE movement of an order line (sign as stored by Inventory).</summary>
public sealed record OrderStockBatch(Guid MovementId, Guid OrderItemId, Guid? BatchId, string? BatchCode, int Quantity, DateTimeOffset OccurredAt);

/// <summary>Payment facts shown on an order (Payments.Public).</summary>
public sealed record OrderPaymentView(string Status, long PaidPaise, long RefundedPaise);

public sealed record ShipmentTrackingView(
    string Status,
    string? RiderName,
    string? RiderPhone,
    DateTimeOffset? DispatchedAt,
    bool ColdBoxUsed,
    string? DeliveryCode,
    IReadOnlyList<TrackingAttemptDto> Attempts);
