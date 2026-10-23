using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Payments.Application.Providers;

/// <summary>What an adapter can do; callers check before calling (PAY-R1, R2).</summary>
[Flags]
public enum ProviderCapabilities
{
    None = 0,
    OnlineCheckout = 1,
    Webhooks = 2,
    ProviderRefunds = 4,
    CollectedOnDelivery = 8,
}

/// <summary>
/// A payment provider adapter (PAY-R1), selected by <see cref="Key"/>. Methods outside the adapter's
/// <see cref="Capabilities"/> throw <see cref="NotSupportedException"/>. Transport errors throw
/// <see cref="PaymentProviderUnavailableException"/>. Never called inside a database transaction.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Stored provider code, e.g. <c>CASH</c>, <c>FAKE</c>; lowercase in webhook URLs.</summary>
    string Key { get; }

    ProviderCapabilities Capabilities { get; }

    Task<ProviderOrder> CreateOrderAsync(CreateProviderOrder request, CancellationToken cancellationToken);

    /// <summary>Checks the webhook signature over the raw body with a constant-time compare.</summary>
    bool VerifySignature(IReadOnlyDictionary<string, string> headers, ReadOnlySpan<byte> rawBody);

    /// <summary>Reads a webhook whose signature is valid; throws <see cref="FormatException"/> when it cannot be read.</summary>
    ProviderWebhook ParseWebhook(IReadOnlyDictionary<string, string> headers, ReadOnlySpan<byte> rawBody);

    Task<ProviderPaymentState> GetPaymentAsync(string providerOrderId, CancellationToken cancellationToken);

    /// <summary>Creates at most one provider refund per <see cref="ProviderRefundRequest.IdempotencyKey"/>.</summary>
    Task<ProviderRefundResult> CreateRefundAsync(ProviderRefundRequest request, CancellationToken cancellationToken);

    Task<ProviderRefundState> GetRefundAsync(string providerRefundId, CancellationToken cancellationToken);
}

/// <param name="PaymentId">Our payment id, sent as the provider receipt.</param>
public sealed record CreateProviderOrder(Guid PaymentId, string OrderNumber, long AmountPaise, string Currency, string? CustomerPhone, DateTimeOffset? ExpiresAt);

/// <param name="CheckoutParameters">What the client needs to open the provider checkout (e.g. <c>keyId</c>).</param>
public sealed record ProviderOrder(string ProviderOrderId, IReadOnlyDictionary<string, string> CheckoutParameters, DateTimeOffset? ExpiresAt)
{
    public string? KeyId => CheckoutParameters.TryGetValue("keyId", out var keyId) ? keyId : null;
}

public enum ProviderWebhookKind
{
    PaymentAuthorized,
    PaymentCaptured,
    PaymentFailed,
    RefundProcessed,
    RefundFailed,
    Other,
}

public sealed record ProviderWebhook(
    string ProviderEventId,
    ProviderWebhookKind Kind,
    string EventType,
    string? ProviderOrderId,
    string? ProviderPaymentId,
    string? ProviderRefundId,
    long? AmountPaise,
    string? Currency,
    string? FailureCode,
    string? FailureMessage,
    string? Instrument);

public enum ProviderAttemptStatus
{
    Created,
    Authorized,
    Captured,
    Failed,
}

public sealed record ProviderPaymentAttempt(string ProviderPaymentId, ProviderAttemptStatus Status, long AmountPaise, string Currency);

public sealed record ProviderPaymentState(string ProviderOrderId, IReadOnlyList<ProviderPaymentAttempt> Attempts);

public sealed record ProviderRefundRequest(string ProviderPaymentId, long AmountPaise, string IdempotencyKey, IReadOnlyDictionary<string, string> Notes);

public sealed record ProviderRefundResult(string? ProviderRefundId, ProviderRefundStatus Status, string? FailureReason);

public sealed record ProviderRefundState(string ProviderRefundId, ProviderRefundStatus Status, string? FailureReason);

/// <summary>The adapter is disabled or the provider cannot be reached (HTTP 503 in requests, retry in jobs).</summary>
public sealed class PaymentProviderUnavailableException : ServiceUnavailableException
{
    public PaymentProviderUnavailableException(string message, Exception? innerException = null)
        : base(PaymentsErrors.PaymentProviderUnavailable, message, innerException)
    {
    }
}

/// <summary>The registered adapters.</summary>
public interface IPaymentProviderCatalog
{
    /// <summary>Any registered adapter (enabled or not), for capability checks; null when unknown.</summary>
    IPaymentProvider? Find(string key);

    /// <summary>An adapter enabled by configuration; null when unknown or disabled.</summary>
    IPaymentProvider? FindEnabled(string key);

    /// <summary>The configured online provider; throws <see cref="PaymentProviderUnavailableException"/> when not configured or disabled.</summary>
    IPaymentProvider GetOnlineProvider();

    ProviderCapabilities CapabilitiesOf(string key);
}
