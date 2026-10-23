using System.Text.Json.Serialization;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Ordering.Public.Events;

// Final payloads of docs/product/ordering.md §4 (D9). Every order event carries orderId, orderNumber, customerId and
// fulfillmentLocationId; codes are UPPER_SNAKE strings so consumers never reference Ordering enums. Quantities are in
// the variant stock unit given by stockUnit (GRAM / PIECE).

/// <summary>One order line in <see cref="OrderPlaced"/>; <c>GrossQuantity</c> is what was reserved.</summary>
public sealed record OrderPlacedLine(
    Guid OrderItemId,
    Guid VariantId,
    string? PreparationCode,
    string StockUnit,
    int YieldBp,
    int OrderedQuantity,
    int GrossQuantity);

/// <summary>The checkout transaction committed a new order (status CONFIRMED or PENDING_PAYMENT).</summary>
public sealed record OrderPlaced(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid CartId,
    Guid CityId,
    Guid ServiceAreaId,
    Guid DeliverySlotId,
    DateTimeOffset SlotStartAt,
    DateTimeOffset SlotEndAt,
    string Status,
    string PaymentMethod,
    DateTimeOffset? PaymentDueAt,
    long AmountDuePaise,
    long EstimatedTotalPaise,
    long MaxTotalPaise,
    long DiscountPaise,
    long DeliveryFeePaise,
    string? CouponCode,
    IReadOnlyList<OrderPlacedLine> Lines,
    DateTimeOffset PlacedAt) : DomainEvent;

/// <summary>PENDING_PAYMENT → CONFIRMED after a captured online payment.</summary>
public sealed record OrderConfirmed(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid PaymentId,
    DateTimeOffset ConfirmedAt) : DomainEvent;

/// <summary>An unpaid ONLINE order failed (reason PAYMENT_TIMEOUT or PAYMENT_FAILED).</summary>
public sealed record OrderPaymentFailed(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid DeliverySlotId,
    string Reason,
    string? CouponCode,
    DateTimeOffset At) : DomainEvent;

/// <summary>The order was cancelled by the customer or staff.</summary>
public sealed record OrderCancelled(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    string PreviousStatus,
    Guid DeliverySlotId,
    string ActorType,
    Guid? ActorId,
    string ReasonCode,
    string PaymentMethod,
    bool StockConsumed,
    string? CouponCode,
    DateTimeOffset CancelledAt) : DomainEvent;

/// <summary>Refund due of a packed prepaid order split by refund reason.</summary>
public sealed record RefundDueByReason(
    [property: JsonPropertyName("WEIGHT_SHORTFALL")] long WeightShortfall,
    [property: JsonPropertyName("SHORT_SUPPLY")] long ShortSupply);

/// <summary>One order line in <see cref="OrderPacked"/> (all lines, including SHORT_SUPPLIED and SUBSTITUTED).</summary>
public sealed record OrderPackedLine(
    Guid OrderItemId,
    Guid VariantId,
    string StockUnit,
    int YieldBp,
    int OrderedQuantity,
    int PackedQuantity,
    int ChargedQuantity,
    long FinalTotalPaise,
    string Status);

/// <summary>Weigh-and-pack committed final amounts.</summary>
public sealed record OrderPacked(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    DateTimeOffset PackedAt,
    bool ColdBoxUsed,
    string PaymentMethod,
    long FinalTotalPaise,
    long AmountDuePaise,
    long RefundDuePaise,
    RefundDueByReason RefundDueByReason,
    IReadOnlyList<OrderPackedLine> Lines) : DomainEvent;

/// <summary>PACKED → OUT_FOR_DELIVERY after the shipment was dispatched.</summary>
public sealed record OrderDispatched(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid ShipmentId,
    Guid? RiderId,
    DateTimeOffset DispatchedAt) : DomainEvent;

/// <summary>OUT_FOR_DELIVERY → DELIVERED; the invoice was issued in the same transaction.</summary>
public sealed record OrderDelivered(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid ShipmentId,
    DateTimeOffset DeliveredAt,
    long FinalTotalPaise,
    long AmountDuePaise,
    Guid InvoiceId,
    string InvoiceNumber) : DomainEvent;

/// <summary>DELIVERY_FAILED → RETURNED.</summary>
public sealed record OrderReturned(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid ShipmentId,
    string PaymentMethod,
    DateTimeOffset ReturnedAt) : DomainEvent;

/// <summary>Raised on every status transition.</summary>
public sealed record OrderStatusChanged(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    string? From,
    string To,
    string ActorType,
    Guid? ActorId,
    string? ReasonCode,
    DateTimeOffset ChangedAt) : DomainEvent;

/// <summary>A GOODWILL or SUBSTITUTION adjustment changed what the customer owes or gets back.</summary>
public sealed record OrderAdjusted(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid AdjustmentId,
    string Kind,
    long AmountPaise,
    long AmountDuePaise,
    long RefundDuePaise,
    string Reason,
    Guid? RequestedBy,
    DateTimeOffset At) : DomainEvent;

/// <summary>A payment was captured for an order that is closed or already paid; Payments refunds it (PAY-R10).</summary>
public sealed record PaymentReceivedForClosedOrder(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid PaymentId,
    string OrderStatus,
    long AmountPaise) : DomainEvent;

/// <summary>Staff proposed a substitute that waits for the customer's answer.</summary>
public sealed record SubstitutionProposed(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid SubstitutionId,
    Guid OrderItemId,
    string OriginalName,
    string ProposedName,
    int ProposedQuantity,
    string StockUnit,
    long PriceDifferencePaise,
    DateTimeOffset ExpiresAt) : DomainEvent;

/// <summary>A substitution was accepted, rejected or expired.</summary>
public sealed record SubstitutionResolved(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid SubstitutionId,
    Guid OrderItemId,
    string Status,
    string ActorType,
    Guid? NewOrderItemId,
    DateTimeOffset At) : DomainEvent;
