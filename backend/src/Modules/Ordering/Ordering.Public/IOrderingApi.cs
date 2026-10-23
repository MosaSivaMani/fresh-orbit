namespace FreshOrbit.Ordering.Public;

/// <summary>What other modules may ask the Ordering module.</summary>
public interface IOrderingApi
{
    Task<OrderSummary?> GetOrderSummaryAsync(Guid orderId, CancellationToken cancellationToken = default);
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
