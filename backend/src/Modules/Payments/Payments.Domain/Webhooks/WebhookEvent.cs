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
/// signature is valid is the duplicate guard (PAY-R7, schema S4). Rejected rows are kept for forensics only.
/// </summary>
public sealed class WebhookEvent : Entity
{
    public const int MaxErrorLength = 500;

    private WebhookEvent()
    {
        Provider = string.Empty;
        ProviderEventId = string.Empty;
        EventType = string.Empty;
        Payload = "{}";
        Headers = "{}";
    }

    private WebhookEvent(string provider, string providerEventId, string eventType, string payload, string headers, bool signatureValid, DateTimeOffset receivedAt)
    {
        Provider = provider;
        ProviderEventId = providerEventId;
        EventType = eventType;
        Payload = payload;
        Headers = headers;
        SignatureValid = signatureValid;
        ReceivedAt = receivedAt;
        Status = signatureValid ? WebhookEventStatus.Received : WebhookEventStatus.Rejected;
    }

    public string Provider { get; private set; }

    public string ProviderEventId { get; private set; }

    public string EventType { get; private set; }

    /// <summary>Raw body as JSON (jsonb); a body that is not JSON is stored as a JSON string.</summary>
    public string Payload { get; private set; }

    /// <summary>Signature and event-id headers only (jsonb).</summary>
    public string Headers { get; private set; }

    public bool SignatureValid { get; private set; }

    public WebhookEventStatus Status { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>UNKNOWN_PAYMENT, AMOUNT_MISMATCH, UNKNOWN_REFUND, …</summary>
    public string? Error { get; private set; }

    public static WebhookEvent Rejected(string provider, string providerEventId, string eventType, string payload, string headers, DateTimeOffset receivedAt)
        => new(provider, providerEventId, eventType, payload, headers, signatureValid: false, receivedAt);

    public static WebhookEvent Received(string provider, string providerEventId, string eventType, string payload, string headers, DateTimeOffset receivedAt)
        => new(provider, providerEventId, eventType, payload, headers, signatureValid: true, receivedAt);

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
