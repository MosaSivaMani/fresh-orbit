namespace FreshOrbit.Payments.Public;

/// <summary>What other modules may ask the Payments module.</summary>
public interface IPaymentsApi
{
    /// <summary>
    /// Payment method codes Payments can take right now: COD and UPI_ON_DELIVERY always, ONLINE only while an online
    /// provider is configured and enabled. Ordering intersects this with the city's order policy.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailablePaymentMethodsAsync(CancellationToken cancellationToken = default);

    /// <summary>Status of the order's latest attempt and the paid / refunded totals.</summary>
    Task<OrderPaymentStatus> GetPaymentStatusAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Payment facts of an order for order detail and complaint decisions (docs/product/payments.md §1).</summary>
    Task<OrderPaymentSummary> GetOrderPaymentSummaryAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>The same for many orders in one query (order lists, customer lifetime value). Every requested id is present.</summary>
    Task<IReadOnlyDictionary<Guid, OrderPaymentSummary>> GetOrderPaymentSummariesAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default);
}

/// <summary>Status of an order's latest payment. Stored as NONE, PENDING, AUTHORIZED, CAPTURED, COLLECTED, FAILED, CANCELLED.</summary>
public enum PaymentStatus
{
    None,
    Pending,
    Authorized,
    Captured,
    Collected,
    Failed,
    Cancelled,
}

public sealed record OrderPaymentStatus(Guid OrderId, PaymentStatus Status, long PaidPaise, long RefundedPaise);

/// <summary>What the customer should do next about payment. Stored as NONE / INITIATE_PAYMENT / PAY_ON_DELIVERY.</summary>
public enum PaymentNextAction
{
    None,
    InitiatePayment,
    PayOnDelivery,
}

/// <param name="Method">COD, UPI_ON_DELIVERY or ONLINE; null when the order has no payment row yet (ONLINE before initiate).</param>
/// <param name="PaidPaise">Captured (online) or collected (on delivery); excess captures are not counted.</param>
/// <param name="PendingRefundPaise">Refunds REQUESTED, APPROVED or PROCESSING.</param>
/// <param name="RefundablePaise">paid − refunds that hold an amount.</param>
public sealed record OrderPaymentSummary(
    Guid OrderId,
    string? Method,
    PaymentStatus LatestStatus,
    long PaidPaise,
    long RefundedPaise,
    long PendingRefundPaise,
    long RefundablePaise,
    PaymentNextAction NextAction);
