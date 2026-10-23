namespace FreshOrbit.Payments.Domain.Shared;

/// <summary>Error codes of the Payments module (docs/api/README.md §2, Payments and Common).</summary>
public static class PaymentsErrors
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFound = "NOT_FOUND";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string LocationAccessDenied = "LOCATION_ACCESS_DENIED";
    public const string PaymentNotRequired = "PAYMENT_NOT_REQUIRED";
    public const string PaymentProviderUnavailable = "PAYMENT_PROVIDER_UNAVAILABLE";
    public const string WebhookSignatureInvalid = "WEBHOOK_SIGNATURE_INVALID";
    public const string WebhookProviderUnknown = "WEBHOOK_PROVIDER_UNKNOWN";
    public const string RefundExceedsPayment = "REFUND_EXCEEDS_PAYMENT";
    public const string RefundNotApprovable = "REFUND_NOT_APPROVABLE";
    public const string RefundSelfApproval = "REFUND_SELF_APPROVAL";
    public const string NoCapturedPayment = "NO_CAPTURED_PAYMENT";
    public const string CashAlreadyCollected = "CASH_ALREADY_COLLECTED";
    public const string SettlementExists = "SETTLEMENT_EXISTS";
    public const string SettlementSelfResolution = "SETTLEMENT_SELF_RESOLUTION";

    // Field-level codes inside VALIDATION_FAILED.
    public const string CashAmountMismatch = "CASH_AMOUNT_MISMATCH";
    public const string RefundMethodNotAllowed = "REFUND_METHOD_NOT_ALLOWED";
    public const string UpiIdRequired = "UPI_ID_REQUIRED";
    public const string UpiReferenceRequired = "UPI_REFERENCE_REQUIRED";
    public const string ComplaintRequired = "COMPLAINT_REQUIRED";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string ReferenceInvalid = "REFERENCE_INVALID";
    public const string AmountInvalid = "AMOUNT_INVALID";
    public const string DateInFuture = "DATE_IN_FUTURE";
}
