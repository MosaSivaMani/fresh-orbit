using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Notifications.Application.Shared;

/// <summary>Why an SMS was not sent. Stored (as <c>SMS_</c> + code) in <c>notifications.messages.last_error</c>.</summary>
public enum SmsFailureKind
{
    /// <summary><c>Notifications:Sms</c> is incomplete (normally refused at start-up).</summary>
    NotConfigured,

    /// <summary>No DLT template id is configured for the template code.</summary>
    TemplateNotRegistered,

    /// <summary>The phone is not an Indian mobile number (DLT covers +91 numbers only).</summary>
    RecipientUnsupported,

    /// <summary>The provider refused the API key.</summary>
    Unauthorized,

    /// <summary>The provider refused this message (DLT scrubbing, sender id, template text, balance, ...).</summary>
    Rejected,

    /// <summary>The provider could not be reached, timed out or answered 429 / 5xx.</summary>
    Unreachable,
}

/// <summary>
/// An SMS could not be sent. In a request it answers 503 <c>SMS_PROVIDER_UNAVAILABLE</c> (D31) with a fixed detail;
/// the Worker records <see cref="FailureCode"/> on the message and retries only when <see cref="IsRetryable"/>.
/// </summary>
public sealed class SmsDeliveryException : ServiceUnavailableException
{
    public SmsDeliveryException(SmsFailureKind kind, string? providerStatus = null, Exception? innerException = null)
        : base(NotificationsErrors.SmsProviderUnavailable, "The SMS could not be sent; try again in a few minutes.", innerException)
    {
        Kind = kind;
        ProviderStatus = providerStatus;
    }

    public SmsFailureKind Kind { get; }

    /// <summary>The provider's own status or error code, when it gave one (never its message text).</summary>
    public string? ProviderStatus { get; }

    /// <summary><c>SMS_</c> + the kind's code, e.g. <c>SMS_TEMPLATE_NOT_REGISTERED</c>.</summary>
    public string FailureCode => "SMS_" + Kind.ToCode();

    /// <summary>Only a provider outage or a refused key can succeed later without changing the message.</summary>
    public bool IsRetryable => Kind is SmsFailureKind.Unreachable or SmsFailureKind.Unauthorized;
}
