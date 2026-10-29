namespace FreshOrbit.Ordering.Public;

/// <summary>What other modules may ask the Ordering module (docs/product/ordering.md §1).</summary>
public interface IOrderingApi
{
    // Members added after Phase 2 have default bodies so existing test doubles still compile.
    private const string NewMemberNote = "Not implemented by this test double.";

    Task<OrderSummary?> GetOrderSummaryAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the customer has an order that is not CANCELLED or PAYMENT_FAILED (first-order coupons, PROMO-R7).
    /// </summary>
    Task<bool> HasQualifyingOrderAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Facts Payments needs to start an online payment (payments.md Q1), or null when the order does not exist.</summary>
    Task<OrderPaymentFacts?> GetOrderPaymentFactsAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>The order's status, or null when the order does not exist (cash collection check).</summary>
    Task<OrderStatus?> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Order numbers of the requested orders in one query (G16: coupon redemptions); missing ids are absent.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetOrderNumbersAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not implemented by this test double.");

    /// <summary>
    /// What the delivery board shows about each shipment's order (AD-1, AD-2), in one query; missing ids are absent.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, OrderDeliveryFacts>> GetOrderDeliveryFactsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>
    /// One page of a customer's orders, newest first (admin customer detail, G6). <paramref name="fulfillmentLocationIds"/>
    /// null = every location; otherwise only orders of those locations (staff location scope).
    /// </summary>
    Task<CustomerOrderPage> ListCustomerOrdersAsync(Guid customerId, IReadOnlyCollection<Guid>? fulfillmentLocationIds, int skip, int take, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>Order counts per customer (customers without orders are absent); location scope as in <see cref="ListCustomerOrdersAsync"/>.</summary>
    Task<IReadOnlyDictionary<Guid, CustomerOrderCounts>> GetCustomerOrderCountsAsync(IReadOnlyCollection<Guid> customerIds, IReadOnlyCollection<Guid>? fulfillmentLocationIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);
}

/// <summary>Stored as PENDING_PAYMENT, CONFIRMED, … (see docs/architecture).</summary>
public enum OrderStatus
{
    PendingPayment,
    Confirmed,
    Preparing,
    Packed,
    OutForDelivery,
    Delivered,
    PaymentFailed,
    Cancelled,
    DeliveryFailed,
    Returned,
}

/// <summary>Stored as COD / UPI_ON_DELIVERY / ONLINE.</summary>
public enum PaymentMethod
{
    Cod,
    UpiOnDelivery,
    Online,
}

/// <param name="TotalPaise">Estimated total at placement.</param>
/// <param name="FinalTotalPaise">Total after packing (actual weights); null until packed.</param>
public sealed record OrderSummary(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    long TotalPaise,
    long? FinalTotalPaise,
    long AmountDuePaise,
    Guid FulfillmentLocationId);

/// <summary>One row of a customer's order history (staff view).</summary>
/// <param name="TotalPaise">Estimated total at placement.</param>
/// <param name="FinalTotalPaise">Total after packing (actual weights); null until packed.</param>
public sealed record CustomerOrderItem(
    Guid OrderId,
    string OrderNumber,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    Guid FulfillmentLocationId,
    DateTimeOffset PlacedAt,
    DateTimeOffset SlotStartAt,
    DateTimeOffset SlotEndAt,
    long TotalPaise,
    long? FinalTotalPaise);

public sealed record CustomerOrderPage(IReadOnlyList<CustomerOrderItem> Items, int TotalCount);

/// <summary>A customer's orders by outcome. PAYMENT_FAILED orders are not counted (never placed for the customer).</summary>
/// <param name="Total">Every order except PAYMENT_FAILED.</param>
/// <param name="Open">PENDING_PAYMENT, CONFIRMED, PREPARING, PACKED or OUT_FOR_DELIVERY.</param>
/// <param name="Delivered">DELIVERED.</param>
/// <param name="Cancelled">CANCELLED.</param>
/// <param name="LastPlacedAt">When the newest counted order was placed.</param>
public sealed record CustomerOrderCounts(int Total, int Open, int Delivered, int Cancelled, DateTimeOffset? LastPlacedAt);

/// <param name="AmountDuePaise">What the customer still owes (estimated total until packed, then final).</param>
/// <param name="PaymentDueAt">ONLINE orders: when an unpaid order expires; null otherwise.</param>
public sealed record OrderPaymentFacts(
    Guid OrderId,
    Guid CustomerId,
    string OrderNumber,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    long AmountDuePaise,
    DateTimeOffset? PaymentDueAt,
    Guid FulfillmentLocationId);

/// <summary>The order facts a dispatcher needs on a shipment row (AD-1, AD-2).</summary>
/// <param name="AddressLine">The delivery address without the pincode: line 1, line 2, landmark, locality and city, comma separated.</param>
/// <param name="AmountToCollectPaise">What the rider collects at the door: the amount due for COD and UPI on delivery, 0 for prepaid orders.</param>
public sealed record OrderDeliveryFacts(
    Guid OrderId,
    string OrderNumber,
    string RecipientName,
    string Phone,
    string AddressLine,
    string Pincode,
    PaymentMethod PaymentMethod,
    long AmountToCollectPaise);
