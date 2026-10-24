using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Payments.Domain.Webhooks;

/// <summary>Stored as RECEIVED / PROCESSED / IGNORED / ERROR / REJECTED.</summary>
public enum WebhookEventStatus
{
    Received,
    Processed,
    Ignored,
    Error,
    Rejected,
}

/// <summary>
/// A provider callback as received. Insert-once: the partial unique index on (provider, provider_event_id) where the
/// signature is valid is the duplicate guard (PAY-R7, schema S4). Rejected rows are kept for forensics only, with at
/// most <see cref="MaxRejectedPayloadBytes"/> of the body plus the SHA-256 of the whole body (SEC-6).
/// </summary>
public sealed class WebhookEvent : Entity
{
    public const int MaxErrorLength = 500;

    /// <summary>SEC-6: largest webhook body accepted at all (64 KB).</summary>
    public const int MaxBodyBytes = 65_536;

    /// <summary>SEC-6: how much of a REJECTED body is kept.</summary>
    public const int MaxRejectedPayloadBytes = 4096;

    /// <summary>Length of a lowercase hex SHA-256.</summary>
    public const int Sha256HexLength = 64;

    private WebhookEvent()
    {
        Provider = string.Empty;
        ProviderEventId = string.Empty;
        EventType = string.Empty;
        Payload = "{}";
        PayloadSha256 = string.Empty;
        Headers = "{}";
    }

    private WebhookEvent(
        string provider,
        string providerEventId,
        string eventType,
        string payload,
        string payloadSha256,
        bool payloadTruncated,
        string headers,
        bool signatureValid,
        DateTimeOffset receivedAt)
    {
        Provider = provider;
        ProviderEventId = providerEventId;
        EventType = eventType;
        Payload = payload;
        PayloadSha256 = payloadSha256;
        PayloadTruncated = payloadTruncated;
        Headers = headers;
        SignatureValid = signatureValid;
        ReceivedAt = receivedAt;
        Status = signatureValid ? WebhookEventStatus.Received : WebhookEventStatus.Rejected;
    }

    public string Provider { get; private set; }

    public string ProviderEventId { get; private set; }

    public string EventType { get; private set; }

    /// <summary>
    /// Raw body as JSON (jsonb); a body that is not JSON is stored as a JSON string. A REJECTED body is always stored as a
    /// JSON string of at most its first <see cref="MaxRejectedPayloadBytes"/> bytes (<see cref="PayloadTruncated"/> when cut).
    /// </summary>
    public string Payload { get; private set; }

    /// <summary>Lowercase hex SHA-256 of the whole raw body as received, also when <see cref="Payload"/> is truncated.</summary>
    public string PayloadSha256 { get; private set; }

    /// <summary>True when only the first <see cref="MaxRejectedPayloadBytes"/> of a REJECTED body were kept.</summary>
    public bool PayloadTruncated { get; private set; }

    /// <summary>Signature and event-id headers only (jsonb).</summary>
    public string Headers { get; private set; }

    public bool SignatureValid { get; private set; }

    public WebhookEventStatus Status { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>UNKNOWN_PAYMENT, AMOUNT_MISMATCH, UNKNOWN_REFUND, …</summary>
    public string? Error { get; private set; }

    public static WebhookEvent Rejected(string provider, string providerEventId, string eventType, string payload, string payloadSha256, bool payloadTruncated, string headers, DateTimeOffset receivedAt)
        => new(provider, providerEventId, eventType, payload, payloadSha256, payloadTruncated, headers, signatureValid: false, receivedAt);

    public static WebhookEvent Received(string provider, string providerEventId, string eventType, string payload, string payloadSha256, string headers, DateTimeOffset receivedAt)
        => new(provider, providerEventId, eventType, payload, payloadSha256, payloadTruncated: false, headers, signatureValid: true, receivedAt);

    public bool CanReprocess => SignatureValid && Status == WebhookEventStatus.Error;

    public void MarkProcessed(DateTimeOffset at) => Complete(WebhookEventStatus.Processed, null, at);

    public void MarkIgnored(DateTimeOffset at) => Complete(WebhookEventStatus.Ignored, null, at);

    public void MarkError(string error, DateTimeOffset at) => Complete(WebhookEventStatus.Error, error, at);

    private void Complete(WebhookEventStatus status, string? error, DateTimeOffset at)
    {
        if (!SignatureValid)
        {
            throw new InvalidOperationException("A rejected webhook event is never processed.");
        }

        Status = status;
        Error = error is { Length: > MaxErrorLength } ? error[..MaxErrorLength] : error;
        ProcessedAt = at;
    }
}
