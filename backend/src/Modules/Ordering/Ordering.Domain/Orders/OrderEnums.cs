namespace FreshOrbit.Ordering.Domain.Orders;

/// <summary>Order status (docs/api §6). Stored as PENDING_PAYMENT, CONFIRMED, …</summary>
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

/// <summary>Stored as CALL_ME / AUTO_SIMILAR / NONE.</summary>
public enum SubstitutionPreference
{
    CallMe,
    AutoSimilar,
    None,
}

/// <summary>Stored as ACTIVE / SHORT_SUPPLIED / SUBSTITUTED / CANCELLED.</summary>
public enum OrderItemStatus
{
    Active,
    ShortSupplied,
    Substituted,
    Cancelled,
}

/// <summary>Stored as WEIGHT / SHORT_SUPPLY / SUBSTITUTION / GOODWILL.</summary>
public enum AdjustmentKind
{
    Weight,
    ShortSupply,
    Substitution,
    Goodwill,
}

/// <summary>Stored as PROPOSED / ACCEPTED / REJECTED / EXPIRED.</summary>
public enum SubstitutionStatus
{
    Proposed,
    Accepted,
    Rejected,
    Expired,
}

/// <summary>Customer cancel reasons (ORD-R40).</summary>
public enum CustomerCancelReason
{
    ChangedMind,
    WrongAddress,
    SlotIssue,
    Other,
}

/// <summary>Staff cancel reasons (ORD-R40).</summary>
public enum StaffCancelReason
{
    OutOfStock,
    CustomerRequest,
    AddressIssue,
    QualityIssue,
    Other,
}

/// <summary>Why an unpaid ONLINE order failed (ORD-R44, R45).</summary>
public enum PaymentFailureReason
{
    PaymentTimeout,
    PaymentFailed,
}
