using System.Text;
using System.Text.Json;
using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Providers;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.Payments.Domain.Webhooks;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Serialization;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Payments.Application.Webhooks;

/// <summary>Receives and reprocesses provider webhooks (PAY-R7, docs/api §7).</summary>
public sealed class WebhookService(
    IPaymentProviderCatalog providers,
    IWebhookEventRepository events,
    PaymentEventApplier applier,
    IPaymentsSession session,
    IClock clock)
{
    private const int MaxDuplicateRaceRetries = 3;

    /// <summary>
    /// Invalid signature → REJECTED row in its own save, then 401. Valid → Read (event exists?) → Decide (applier) →
    /// Commit event row + payment/refund change + outbox once. A unique violation or lost race means another delivery of
    /// the same event won: the command re-reads and answers <c>duplicate: true</c>.
    /// </summary>
    public async Task<WebhookReceipt> ReceiveAsync(string providerKey, IReadOnlyDictionary<string, string> headers, byte[] rawBody, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rawBody);
        var provider = providers.FindEnabled(providerKey.ToUpperInvariant());
        if (provider is null || !provider.Capabilities.HasFlag(ProviderCapabilities.Webhooks))
        {
            throw new NotFoundException(PaymentsErrors.WebhookProviderUnknown, $"No webhook provider '{providerKey}' is configured.");
        }

        var now = clock.UtcNow;
        var payload = ToJsonPayload(rawBody);
        var storedHeaders = SelectHeaders(headers);
        if (!provider.VerifySignature(headers, rawBody))
        {
            var (eventId, eventType) = TryReadIdentity(provider, headers, rawBody);
            events.Add(WebhookEvent.Rejected(provider.Key, eventId, eventType, payload, storedHeaders, now));
            await session.SaveChangesAsync(cancellationToken);
            throw new WebhookSignatureInvalidException();
        }

        ProviderWebhook webhook;
        try
        {
            webhook = provider.ParseWebhook(headers, rawBody);
        }
        catch (FormatException exception)
        {
            throw new DomainException(PaymentsErrors.ValidationFailed, "The webhook body cannot be read: " + exception.Message);
        }

        for (var attempt = 0; ; attempt++)
        {
            if (await events.ExistsAsync(provider.Key, webhook.ProviderEventId, cancellationToken))
            {
                return new WebhookReceipt(true, Duplicate: true);
            }

            var row = WebhookEvent.Received(provider.Key, webhook.ProviderEventId, webhook.EventType, payload, storedHeaders, now);
            events.Add(row);
            var outcome = await applier.ApplyAsync(provider.Key, webhook, cancellationToken);
            Complete(row, outcome);
            try
            {
                await session.SaveChangesAsync(cancellationToken);
                return new WebhookReceipt(true, Duplicate: false);
            }
            catch (Exception exception) when (exception is UniqueViolationException or ConcurrentSaveException && attempt < MaxDuplicateRaceRetries)
            {
                session.Reset();
            }
        }
    }

    /// <summary>Applies a stored, signature-valid ERROR event again (e.g. after the payment row was fixed).</summary>
    public async Task<WebhookEventResponse> ReprocessAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var row = await events.GetAsync(eventId, cancellationToken)
            ?? throw new NotFoundException("WEBHOOK_EVENT_NOT_FOUND", "No webhook event has this id.");
        if (!row.CanReprocess)
        {
            throw new ConflictException(PaymentsErrors.InvalidStatusTransition, "Only a signature-valid webhook event in ERROR can be reprocessed.");
        }

        var provider = providers.Find(row.Provider)
            ?? throw new NotFoundException(PaymentsErrors.WebhookProviderUnknown, $"No provider '{row.Provider}' is registered.");
        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(row.Headers, FreshOrbitJson.Options) ?? [];
        var webhook = provider.ParseWebhook(new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase), Encoding.UTF8.GetBytes(row.Payload));
        var outcome = await applier.ApplyAsync(provider.Key, webhook, cancellationToken);
        Complete(row, outcome);
        await session.SaveChangesAsync(cancellationToken);
        return row.ToResponse();
    }

    private void Complete(WebhookEvent row, ApplyOutcome outcome)
    {
        var now = clock.UtcNow;
        switch (outcome.Status)
        {
            case WebhookEventStatus.Processed:
                row.MarkProcessed(now);
                break;
            case WebhookEventStatus.Ignored:
                row.MarkIgnored(now);
                break;
            default:
                row.MarkError(outcome.Error ?? "ERROR", now);
                break;
        }
    }

    private static (string EventId, string EventType) TryReadIdentity(IPaymentProvider provider, IReadOnlyDictionary<string, string> headers, byte[] rawBody)
    {
        try
        {
            var parsed = provider.ParseWebhook(headers, rawBody);
            return (Truncate(parsed.ProviderEventId), Truncate(parsed.EventType));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return ("UNREADABLE", "UNREADABLE");
        }
    }

    private static string Truncate(string value) => value.Length <= 200 ? value : value[..200];

    /// <summary>Raw body as jsonb: the body itself when it is JSON, otherwise a JSON string.</summary>
    private static string ToJsonPayload(byte[] rawBody)
    {
        var text = Encoding.UTF8.GetString(rawBody);
        try
        {
            using var document = JsonDocument.Parse(text);
            return text;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(text, FreshOrbitJson.Options);
        }
    }

    /// <summary>Only signature and event-id headers are stored (schema S4).</summary>
    private static string SelectHeaders(IReadOnlyDictionary<string, string> headers)
    {
        var selected = headers
            .Where(h => h.Key.Contains("signature", StringComparison.OrdinalIgnoreCase) || h.Key.Contains("event-id", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(h => h.Key, h => h.Value, StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.Serialize(selected, FreshOrbitJson.Options);
    }
}
