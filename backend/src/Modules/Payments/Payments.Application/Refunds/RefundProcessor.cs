using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Providers;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Payments.Application.Refunds;

/// <summary>
/// Worker step for an APPROVED ORIGINAL refund (PAY-R16): tx1 APPROVED → PROCESSING; provider call with the refund's
/// fixed idempotency key outside any transaction; tx2 stores the result. A transient provider error propagates so the
/// caller retries; the re-entry finds PROCESSING without a provider refund id and calls again with the same key.
/// </summary>
public sealed class RefundProcessor(IPaymentRepository payments, IPaymentProviderCatalog providers, IPaymentsSession session, IClock clock)
{
    public async Task ProcessAsync(Guid refundId, CancellationToken cancellationToken)
    {
        var payment = await payments.GetByRefundIdAsync(refundId, cancellationToken);
        if (payment is null || !payment.StartRefundProcessing(refundId))
        {
            return;
        }

        var provider = providers.Find(payment.Provider);
        if (provider is null || !provider.Capabilities.HasFlag(ProviderCapabilities.ProviderRefunds) || payment.ProviderPaymentId is null)
        {
            payment.FailRefund(refundId, "PROVIDER_REFUNDS_NOT_SUPPORTED");
            await session.SaveChangesAsync(cancellationToken);
            return;
        }

        await session.SaveChangesAsync(cancellationToken);

        var refund = payment.GetRefund(refundId);
        var request = new ProviderRefundRequest(
            payment.ProviderPaymentId,
            refund.AmountPaise,
            refund.IdempotencyKey,
            new Dictionary<string, string>
            {
                ["refundId"] = refund.Id.ToString(),
                ["orderId"] = refund.OrderId.ToString(),
                ["idempotencyKey"] = refund.IdempotencyKey,
            });
        var result = await provider.CreateRefundAsync(request, cancellationToken);

        await SaveRetry.RunAsync(session, async () =>
        {
            var current = await payments.GetByRefundIdAsync(refundId, cancellationToken);
            current?.ApplyProviderRefundResult(refundId, result.ProviderRefundId, result.Status, result.FailureReason, clock.UtcNow);
            await session.SaveChangesAsync(cancellationToken);
            return true;
        });
    }

    /// <summary>Applies a provider refund state found by reconciliation (PAY-R21 b).</summary>
    public static void ApplyState(Payment payment, Refund refund, ProviderRefundState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(refund);
        ArgumentNullException.ThrowIfNull(state);
        payment.ApplyProviderRefundResult(refund.Id, state.ProviderRefundId, state.Status, state.FailureReason, now);
    }
}
