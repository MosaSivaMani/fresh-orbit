namespace FreshOrbit.Ordering.Domain.Shared;

/// <summary>Error codes of the Ordering module (docs/api §2 Ordering / Checkout and docs/product/ordering.md).</summary>
public static class OrderingErrors
{
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string CartEmpty = "CART_EMPTY";
    public const string CartLocationChanged = "CART_LOCATION_CHANGED";
    public const string AddressNotServiceable = "ADDRESS_NOT_SERVICEABLE";
    public const string MinOrderNotMet = "MIN_ORDER_NOT_MET";
    public const string PaymentMethodNotAllowed = "PAYMENT_METHOD_NOT_ALLOWED";
    public const string CodLimitExceeded = "COD_LIMIT_EXCEEDED";
    public const string PriceChanged = "PRICE_CHANGED";
    public const string SlotFull = "SLOT_FULL";
    public const string SlotCutoffPassed = "SLOT_CUTOFF_PASSED";
    public const string SlotNotAvailable = "SLOT_NOT_AVAILABLE";
    public const string OutOfStock = "OUT_OF_STOCK";
    public const string OrderNotCancellable = "ORDER_NOT_CANCELLABLE";
    public const string PackedQuantityInvalid = "PACKED_QUANTITY_INVALID";
    public const string SubstitutionNotPending = "SUBSTITUTION_NOT_PENDING";
    public const string SubstitutionExpired = "SUBSTITUTION_EXPIRED";
    public const string SubstitutionNotAllowed = "SUBSTITUTION_NOT_ALLOWED";
    public const string ComplaintWindowClosed = "COMPLAINT_WINDOW_CLOSED";
    public const string ComplaintAlreadyOpen = "COMPLAINT_ALREADY_OPEN";
    public const string ComplaintPhotoLimitReached = "COMPLAINT_PHOTO_LIMIT_REACHED";
    public const string ImageInvalid = "IMAGE_INVALID";
    public const string InvoiceNotIssued = "INVOICE_NOT_ISSUED";
    public const string LoginRequired = "LOGIN_REQUIRED";
    public const string CustomerDeleted = "CUSTOMER_DELETED";
    public const string LocationAccessDenied = "LOCATION_ACCESS_DENIED";
    public const string ConfigurationMissing = "CONFIGURATION_MISSING";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string GoodwillExceedsTotal = "GOODWILL_EXCEEDS_TOTAL";
    public const string RefundAmountInvalid = "REFUND_AMOUNT_INVALID";
    public const string ConcurrentSave = "CONCURRENT_SAVE";
    public const string NotFound = "NOT_FOUND";
}
