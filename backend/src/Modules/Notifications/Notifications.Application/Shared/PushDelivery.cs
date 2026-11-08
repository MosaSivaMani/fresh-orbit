using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;

namespace FreshOrbit.Notifications.Application.Shared;

/// <summary>Why a push was not sent. Stored (as <c>PUSH_</c> + code) in <c>notifications.messages.last_error</c>.</summary>
public enum PushFailureKind
{
    /// <summary><c>Notifications:Push:Fcm</c> is incomplete (normally refused at start-up).</summary>
    NotConfigured,

    /// <summary>The provider refused our credentials (service account, project id, or the API is not enabled).</summary>
    Unauthorized,

    /// <summary>The provider refused this message (payload too large, invalid argument, ...).</summary>
    Rejected,

    /// <summary>The provider could not be reached, timed out or answered 429 / 5xx.</summary>
    Unreachable,
}

/// <summary>
/// No device accepted a push (D324). The Worker removes <see cref="InvalidTokens"/>, records <see cref="FailureCode"/>
/// on the message and retries only when <see cref="IsRetryable"/>.
/// </summary>
public sealed class PushDeliveryException : Exception
{
    public PushDeliveryException(PushFailureKind kind, IReadOnlyList<string>? invalidTokens = null, string? providerStatus = null, Exception? innerException = null)
        : base("The push notification could not be sent (PUSH_" + kind.ToCode() + ").", innerException)
    {
        Kind = kind;
        InvalidTokens = invalidTokens ?? [];
        ProviderStatus = providerStatus;
    }

    public PushFailureKind Kind { get; }

    /// <summary>Tokens the provider reported as no longer registered while trying; they are removed all the same.</summary>
    public IReadOnlyList<string> InvalidTokens { get; }

    /// <summary>The provider's own status or error code, when it gave one (never its message text).</summary>
    public string? ProviderStatus { get; }

    /// <summary><c>PUSH_</c> + the kind's code, e.g. <c>PUSH_UNREACHABLE</c>.</summary>
    public string FailureCode => "PUSH_" + Kind.ToCode();

    /// <summary>A provider outage or a refused credential can succeed later without changing the message.</summary>
    public bool IsRetryable => Kind is PushFailureKind.Unreachable or PushFailureKind.Unauthorized;
}

/// <summary>
/// D322 / D325: the channel is switched off by configuration (<c>Notifications:Email:Provider</c> or
/// <c>Notifications:Push:Provider</c> = <c>DISABLED</c>). The message fails with <see cref="FailureCode"/>
/// (<c>EMAIL_DISABLED</c> or <c>PUSH_DISABLED</c>) and is never marked SENT.
/// </summary>
public sealed class ChannelDisabledException : Exception
{
    public ChannelDisabledException(Channel channel)
        : base($"The {channel.ToCode()} channel is disabled by configuration.")
    {
        Channel = channel;
    }

    public Channel Channel { get; }

    /// <summary><c>EMAIL_DISABLED</c> or <c>PUSH_DISABLED</c>.</summary>
    public string FailureCode => Channel.ToCode() + "_DISABLED";
}
