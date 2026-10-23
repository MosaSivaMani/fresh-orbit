using System.Text.Json;
using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Providers;
using FreshOrbit.Payments.Application.Refunds;
using FreshOrbit.Payments.Application.Webhooks;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.Payments.Domain.Webhooks;
using FreshOrbit.SharedKernel.Serialization;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Payments.Application.Reconciliation;

/// <summary>What one reconciliation run did.</summary>
public sealed record ReconciliationResult(int ProviderCalls, int PaymentsCaptured, int RefundsUpdated, int IssuesOpened, int IssuesAutoResolved);

/// <summary>
/// Hourly reconciliation (PAY-R21). One read per check, provider HTTP calls in a capped loop, one commit.
/// Issues are kept unique per (kind, subject) in memory against the OPEN issues read once at the start; an OPEN issue
/// whose condition is gone is AUTO_RESOLVED.
/// </summary>
public sealed class ReconciliationService(
    IPaymentRepository payments,
    IWebhookEventRepository events,
    ICashSettlementRepository settlements,
    IReconciliationIssueRepository issues,
    IPaymentProviderCatalog providers,
    PaymentEventApplier applier,
    IPaymentsSession session,
    PaymentsOptions options,
    IClock clock)
{
    private static readonly IssueKind[] DetectedKinds =
    [
        IssueKind.AmountMismatch, IssueKind.RefundStuck, IssueKind.DeliveredNotCollected, IssueKind.UnsettledCash,
        IssueKind.SettlementAged, IssueKind.WebhookError,
    ];

    private static readonly IssueKind[] AutoResolvedKinds =
    [
        IssueKind.RefundStuck, IssueKind.DeliveredNotCollected, IssueKind.UnsettledCash, IssueKind.SettlementAged, IssueKind.WebhookError,
    ];

    public async Task<ReconciliationResult> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        // Read: one query per check.
        var openAttempts = await payments.ListOpenOnlineAttemptsAsync(now.AddHours(-72), now.AddMinutes(-15), options.ReconciliationProviderCallLimit, cancellationToken);
        var processing = await payments.ListWithProcessingRefundsAsync(now.AddMinutes(-30), options.ReconciliationProviderCallLimit, cancellationToken);
        var notCollected = await payments.ListDeliveredNotCollectedAsync(now.AddHours(-2), cancellationToken);
        var unsettled = await payments.ListUnsettledCashBeforeAsync(now.AddHours(-24), cancellationToken);
        var aged = await settlements.ListUnresolvedBeforeAsync(DateOnly.FromDateTime(now.UtcDateTime.AddDays(-3)), cancellationToken);
        var errorEvents = await events.ListErrorsAsync(500, cancellationToken);
        var open = await issues.ListOpenAsync(DetectedKinds, cancellationToken);
        var siblings = openAttempts.Count == 0
            ? []
            : await payments.ListByOrdersAsync([.. openAttempts.Select(p => p.OrderId).Distinct()], cancellationToken);

        var book = new IssueBook(open, issues, now);
        var calls = 0;
        var captured = 0;
        var refundsUpdated = 0;

        // (a) online attempts the provider may have captured without telling us.
        foreach (var attempt in openAttempts)
        {
            if (calls >= options.ReconciliationProviderCallLimit || attempt.ProviderOrderId is null || providers.FindEnabled(attempt.Provider) is not { } provider)
            {
                continue;
            }

            calls++;
            var state = await provider.GetPaymentAsync(attempt.ProviderOrderId, cancellationToken);
            var capture = state.Attempts.FirstOrDefault(a => a.Status == ProviderAttemptStatus.Captured);
            if (capture is null)
            {
                continue;
            }

            var orderPayments = siblings.Where(p => p.OrderId == attempt.OrderId).ToList();
            if (!attempt.Matches(capture.AmountPaise, capture.Currency))
            {
                book.Open(IssueKind.AmountMismatch, IssueSubjectType.Payment, attempt.Id, attempt.OrderId, attempt.FulfillmentLocationId, new { expectedPaise = attempt.AmountPaise, receivedPaise = capture.AmountPaise, capture.Currency, capture.ProviderPaymentId });
                continue;
            }

            var eventId = $"recon:{capture.ProviderPaymentId}:CAPTURED";
            var row = WebhookEvent.Received(provider.Key, eventId, "reconciliation.payment.captured", Serialize(capture), "{}", now);
            events.Add(row);
            await applier.CaptureAsync(attempt, orderPayments, capture.ProviderPaymentId, capture.AmountPaise, capture.Currency, cancellationToken);
            row.MarkProcessed(now);
            captured++;
            book.Record(IssueKind.MissedWebhook, IssueSubjectType.Payment, attempt.Id, attempt.OrderId, attempt.FulfillmentLocationId, new { eventId, capture.ProviderPaymentId });
        }

        // (b) refunds PROCESSING for more than 30 minutes.
        var stuck = new HashSet<Guid>();
        foreach (var payment in processing)
        {
            foreach (var refund in payment.Refunds.Where(r => r.Status == RefundStatus.Processing && r.ApprovedAt < now.AddMinutes(-30)))
            {
                if (refund.ProviderRefundId is null || providers.FindEnabled(payment.Provider) is not { } provider)
                {
                    stuck.Add(refund.Id);
                    book.Open(IssueKind.RefundStuck, IssueSubjectType.Refund, refund.Id, payment.OrderId, payment.FulfillmentLocationId, new { refundId = refund.Id, refund.AttemptCount });
                    continue;
                }

                if (calls >= options.ReconciliationProviderCallLimit)
                {
                    continue;
                }

                calls++;
                var state = await provider.GetRefundAsync(refund.ProviderRefundId, cancellationToken);
                RefundProcessor.ApplyState(payment, refund, state, now);
                if (refund.Status != RefundStatus.Processing)
                {
                    refundsUpdated++;
                }
                else
                {
                    stuck.Add(refund.Id);
                }
            }
        }

        // (c) delivered more than 2 h ago and still not collected.
        foreach (var payment in notCollected)
        {
            book.Open(IssueKind.DeliveredNotCollected, IssueSubjectType.Payment, payment.Id, payment.OrderId, payment.FulfillmentLocationId, new { amountDuePaise = payment.AmountPaise, payment.OrderDeliveredAt });
        }

        // (d) cash collections not settled (older than a day).
        foreach (var payment in unsettled)
        {
            book.Open(IssueKind.UnsettledCash, IssueSubjectType.Payment, payment.Id, payment.OrderId, payment.FulfillmentLocationId, new { collectedPaise = payment.PaidPaise, payment.CollectedAt, riderId = payment.CollectedByRiderId });
        }

        // (e) settlements OPEN/DISPUTED for more than 3 days.
        foreach (var settlement in aged)
        {
            book.Open(IssueKind.SettlementAged, IssueSubjectType.Settlement, settlement.Id, null, settlement.FulfillmentLocationId, new { settlement.RiderId, date = settlement.SettlementDate, status = settlement.Status });
        }

        // (f) webhook events in ERROR (amount mismatches already have their own issue).
        foreach (var errorEvent in errorEvents.Where(e => e.Error != PaymentEventApplier.AmountMismatch))
        {
            book.Open(IssueKind.WebhookError, IssueSubjectType.WebhookEvent, errorEvent.Id, null, null, new { errorEvent.Provider, errorEvent.ProviderEventId, errorEvent.Error });
        }

        // Auto-resolve OPEN issues whose condition is gone.
        var stillTrue = new HashSet<(IssueKind, Guid)>(
            stuck.Select(id => (IssueKind.RefundStuck, id))
                .Concat(notCollected.Select(p => (IssueKind.DeliveredNotCollected, p.Id)))
                .Concat(unsettled.Select(p => (IssueKind.UnsettledCash, p.Id)))
                .Concat(aged.Select(s => (IssueKind.SettlementAged, s.Id)))
                .Concat(errorEvents.Select(e => (IssueKind.WebhookError, e.Id))));
        var resolved = book.AutoResolve(AutoResolvedKinds, stillTrue);

        if (book.Opened > 0 || resolved > 0 || captured > 0 || refundsUpdated > 0 || book.Recorded > 0)
        {
            await session.SaveChangesAsync(cancellationToken);
        }

        return new ReconciliationResult(calls, captured, refundsUpdated, book.Opened, resolved);
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value, FreshOrbitJson.Options);

    /// <summary>OPEN issues read once; opens new ones without duplicates and auto-resolves stale ones.</summary>
    private sealed class IssueBook(IReadOnlyList<ReconciliationIssue> open, IReconciliationIssueRepository repository, DateTimeOffset now)
    {
        private readonly Dictionary<(IssueKind, Guid), ReconciliationIssue> _open = open.ToDictionary(i => (i.Kind, i.SubjectId));

        public int Opened { get; private set; }

        public int Recorded { get; private set; }

        public void Open(IssueKind kind, IssueSubjectType subjectType, Guid subjectId, Guid? orderId, Guid? locationId, object details)
        {
            if (_open.ContainsKey((kind, subjectId)))
            {
                return;
            }

            var issue = ReconciliationIssue.Open(kind, subjectType, subjectId, orderId, locationId, IssueRecorder.Serialize(details), now);
            repository.Add(issue);
            _open[(kind, subjectId)] = issue;
            Opened++;
        }

        public void Record(IssueKind kind, IssueSubjectType subjectType, Guid subjectId, Guid? orderId, Guid? locationId, object details)
        {
            repository.Add(ReconciliationIssue.Recorded(kind, subjectType, subjectId, orderId, locationId, IssueRecorder.Serialize(details), now));
            Recorded++;
        }

        public int AutoResolve(IReadOnlyCollection<IssueKind> kinds, HashSet<(IssueKind, Guid)> stillTrue)
        {
            var count = 0;
            foreach (var ((kind, subjectId), issue) in _open)
            {
                if (kinds.Contains(kind) && !stillTrue.Contains((kind, subjectId)) && issue.Status == IssueStatus.Open && issue.Version != 0)
                {
                    issue.AutoResolve(now);
                    count++;
                }
            }

            return count;
        }
    }
}
