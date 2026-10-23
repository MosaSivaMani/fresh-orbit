namespace FreshOrbit.Payments.Public;

/// <summary>What other modules may ask the Payments module.</summary>
public interface IPaymentsApi
{
    Task<OrderPaymentStatus> GetPaymentStatusAsync(Guid orderId, CancellationToken cancellationToken = default);
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
