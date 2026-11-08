using FreshOrbit.Payments.Domain.Webhooks;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Payments.Infrastructure.Jobs;

/// <summary>
/// Daily retention purge of webhook bodies (SEC-13, D335). A webhook event received more than
/// <see cref="PayloadRetention"/> ago whose work is finished (PROCESSED, IGNORED or REJECTED) loses its raw body and
/// headers, which hold provider PII (VPA, e-mail, contact). The summary columns stay (provider, event id, type, status,
/// SHA-256 of the body, received and processed times, error), and so does the row, so the duplicate guard on
/// (provider, provider_event_id) still refuses a late redelivery. RECEIVED and ERROR rows are kept whole: staff may still
/// reprocess them. One set-based update, no rows loaded; a purged row is never touched again.
/// </summary>
public sealed class WebhookPayloadRetentionJob(FreshOrbitDbContext db, IClock clock) : IRecurringJob
{
    public const string JobId = "payments.purge-webhook-payloads";

    /// <summary>Well past the 72-hour reconciliation window (PAY-R21) and the refund follow-ups.</summary>
    public static readonly TimeSpan PayloadRetention = TimeSpan.FromDays(30);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.UtcNow - PayloadRetention;
        await db.Set<WebhookEvent>()
            .Where(e => e.ReceivedAt < cutoff
                && (e.Status == WebhookEventStatus.Processed || e.Status == WebhookEventStatus.Ignored || e.Status == WebhookEventStatus.Rejected)
                && e.Payload != WebhookEvent.PurgedPayload)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(e => e.Payload, WebhookEvent.PurgedPayload)
                    .SetProperty(e => e.Headers, WebhookEvent.PurgedHeaders),
                cancellationToken);
    }
}
