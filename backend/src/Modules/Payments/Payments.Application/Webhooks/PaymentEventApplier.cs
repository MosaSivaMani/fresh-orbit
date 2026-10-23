using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Providers;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.Payments.Domain.Webhooks;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Payments.Application.Webhooks;

/// <summary>How a provider event was applied: the status and error to store on the webhook event row.</summary>
public sealed record ApplyOutcome(WebhookEventStatus Status, string? Error)
{
    public static readonly ApplyOutcome Processed = new(WebhookEventStatus.Processed, null);
    public static readonly ApplyOutcome Ignored = new(WebhookEventStatus.Ignored, null);

    public static ApplyOutcome Failed(string error) => new(WebhookEventStatus.Error, error);
}

/// <summary>
/// Decides what a provider event means for our payments and refunds (PAY-R7, R9, R11) and changes the tracked
/// aggregates; the caller saves once together with the event row.
/// </summary>
public sealed class PaymentEventApplier(IPaymentRepository payments, IssueRecorder issues, IClock clock)
{
    public const string UnknownPayment = "UNKNOWN_PAYMENT";
    public const string UnknownRefund = "UNKNOWN_REFUND";
    public const string AmountMismatch = "AMOUNT_MISMATCH";

    public async Task<ApplyOutcome> ApplyAsync(string provider, ProviderWebhook webhook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webhook);
        switch (webhook.Kind)
        {
            case ProviderWebhookKind.PaymentCaptured:
            case ProviderWebhookKind.PaymentAuthorized:
            case ProviderWebhookKind.PaymentFailed:
                var payment = await FindPaymentAsync(provider, webhook, cancellationToken);
                if (payment is null)
                {
                    return ApplyOutcome.Failed(UnknownPayment);
                }

                var siblings = await payments.ListByOrderAsync(payment.OrderId, cancellationToken);
                return webhook.Kind switch
                {
                    ProviderWebhookKind.PaymentCaptured => await CaptureAsync(payment, siblings, webhook.ProviderPaymentId, webhook.AmountPaise, webhook.Currency, cancellationToken),
                    ProviderWebhookKind.PaymentAuthorized => Authorize(payment, webhook),
                    _ => Fail(payment, siblings, webhook),
                };
            case ProviderWebhookKind.RefundProcessed:
            case ProviderWebhookKind.RefundFailed:
                return await ApplyRefundAsync(provider, webhook, cancellationToken);
            default:
                return ApplyOutcome.Ignored;
        }
    }

    /// <summary>
    /// Captures <paramref name="payment"/> when the amount matches; otherwise keeps it PENDING and opens an
    /// AMOUNT_MISMATCH issue (PAY-R9). <paramref name="siblings"/> are all attempts of the order (to detect excess money).
    /// </summary>
    public async Task<ApplyOutcome> CaptureAsync(
        Payment payment,
        IReadOnlyList<Payment> siblings,
        string? providerPaymentId,
        long? amountPaise,
        string? currency,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(siblings);
        if (payment.Status == PaymentStatus.Captured)
        {
            return ApplyOutcome.Processed;
        }

        if (amountPaise is not { } amount || !payment.Matches(amount, currency))
        {
            await issues.OpenAsync(
                IssueKind.AmountMismatch,
                IssueSubjectType.Payment,
                payment.Id,
                payment.OrderId,
                payment.FulfillmentLocationId,
                new { expectedPaise = payment.AmountPaise, receivedPaise = amountPaise, currency, providerPaymentId },
                cancellationToken);
            return ApplyOutcome.Failed(AmountMismatch);
        }

        var alreadyPaid = siblings.Any(p => p.Id != payment.Id && p.IsPaid && !p.IsExcess);
        payment.Capture(providerPaymentId, clock.UtcNow, alreadyPaid);
        return ApplyOutcome.Processed;
    }

    private ApplyOutcome Authorize(Payment payment, ProviderWebhook webhook)
    {
        payment.Authorize(webhook.ProviderPaymentId, clock.UtcNow);
        return ApplyOutcome.Processed;
    }

    private ApplyOutcome Fail(Payment payment, IReadOnlyList<Payment> siblings, ProviderWebhook webhook)
    {
        var now = clock.UtcNow;
        var hasOtherActiveAttempt = siblings.Any(p => p.Id != payment.Id && p.IsOpenAttempt && (p.ExpiresAt is null || p.ExpiresAt > now));
        payment.Fail(webhook.ProviderPaymentId, webhook.FailureCode, webhook.FailureMessage, hasOtherActiveAttempt);
        return ApplyOutcome.Processed;
    }

    private async Task<ApplyOutcome> ApplyRefundAsync(string provider, ProviderWebhook webhook, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(webhook.ProviderRefundId))
        {
            return ApplyOutcome.Failed(UnknownRefund);
        }

        var payment = await payments.FindByProviderRefundIdAsync(provider, webhook.ProviderRefundId, cancellationToken);
        var refund = payment?.Refunds.FirstOrDefault(r => r.ProviderRefundId == webhook.ProviderRefundId);
        if (payment is null || refund is null)
        {
            return ApplyOutcome.Failed(UnknownRefund);
        }

        if (webhook.Kind == ProviderWebhookKind.RefundProcessed)
        {
            payment.CompleteRefund(refund.Id, clock.UtcNow);
        }
        else
        {
            payment.FailRefund(refund.Id, webhook.FailureMessage ?? webhook.FailureCode ?? "PROVIDER_FAILED");
        }

        return ApplyOutcome.Processed;
    }

    private async Task<Payment?> FindPaymentAsync(string provider, ProviderWebhook webhook, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(webhook.ProviderPaymentId)
            && await payments.FindByProviderPaymentIdAsync(provider, webhook.ProviderPaymentId, cancellationToken) is { } byPaymentId)
        {
            return byPaymentId;
        }

        return string.IsNullOrEmpty(webhook.ProviderOrderId)
            ? null
            : await payments.FindByProviderOrderIdAsync(provider, webhook.ProviderOrderId, cancellationToken);
    }
}
