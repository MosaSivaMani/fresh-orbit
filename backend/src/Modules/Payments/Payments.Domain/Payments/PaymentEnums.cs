namespace FreshOrbit.Payments.Domain.Payments;

/// <summary>How the order is paid. Stored as COD / UPI_ON_DELIVERY / ONLINE.</summary>
public enum PaymentMethod
{
    Cod,
    UpiOnDelivery,
    Online,
}

/// <summary>Stored as PENDING / AUTHORIZED / CAPTURED / COLLECTED / FAILED / CANCELLED.</summary>
public enum PaymentStatus
{
    Pending,
    Authorized,
    Captured,
    Collected,
    Failed,
    Cancelled,
}

/// <summary>How a delivery payment was collected. Stored as CASH / UPI.</summary>
public enum CollectionMode
{
    Cash,
    Upi,
}

/// <summary>Stored as REQUESTED / APPROVED / PROCESSING / SUCCEEDED / FAILED.</summary>
public enum RefundStatus
{
    Requested,
    Approved,
    Processing,
    Succeeded,
    Failed,
}

/// <summary>Why money goes back. Stored as UPPER_SNAKE codes.</summary>
public enum RefundReason
{
    WeightShortfall,
    Cancellation,
    Complaint,
    ShortSupply,
    Goodwill,
    Return,
}

/// <summary>How a refund is paid out. Stored as ORIGINAL / UPI / BANK_TRANSFER / CASH.</summary>
public enum RefundMethod
{
    Original,
    Upi,
    BankTransfer,
    Cash,
}

/// <summary>A provider's answer to a refund request.</summary>
public enum ProviderRefundStatus
{
    Processed,
    Pending,
    Failed,
}
