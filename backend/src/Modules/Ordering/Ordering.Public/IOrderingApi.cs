namespace FreshOrbit.Ordering.Public;

/// <summary>What other modules may ask the Ordering module (docs/product/ordering.md §1).</summary>
public interface IOrderingApi
{
    Task<OrderSummary?> GetOrderSummaryAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the customer has an order that is not CANCELLED or PAYMENT_FAILED (first-order coupons, PROMO-R7).
    /// </summary>
    Task<bool> HasQualifyingOrderAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Facts Payments needs to start an online payment (payments.md Q1), or null when the order does not exist.</summary>
    Task<OrderPaymentFacts?> GetOrderPaymentFactsAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>The order's status, or null when the order does not exist (cash collection check).</summary>
    Task<OrderStatus?> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken = default);
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
