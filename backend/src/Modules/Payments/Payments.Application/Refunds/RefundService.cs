using FreshOrbit.Ordering.Public;
using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Providers;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Payments.Application.Refunds;

/// <summary>Staff refund commands (PAY-R12..R14, R17; docs/api §4.16).</summary>
public sealed class RefundService(
    IPaymentRepository payments,
    IOrderingApi ordering,
    IPaymentProviderCatalog providers,
    IPaymentsSession session,
    ICurrentActor actor,
    IClock clock)
{
    /// <summary>
    /// Read: the order's paid payment with its refunds. Decide: PAY-R12/R13 in the aggregate. Commit: refund REQUESTED
    /// plus payment summary columns. A lost race on the payment row version is retried from Read (max 3).
    /// </summary>
    public Task<RefundResponse> RequestAsync(Guid orderId, CreateRefundRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestedBy = LocationGuard.RequireUser(actor);
        return SaveRetry.RunAsync(session, async () =>
        {
            var attempts = await payments.ListByOrderAsync(orderId, cancellationToken);
            if (attempts.Count == 0)
            {
                var order = await ordering.GetOrderSummaryAsync(orderId, cancellationToken)
                    ?? throw new NotFoundException("ORDER_NOT_FOUND", "No order has this id.");
                LocationGuard.EnsureAccess(actor, order.FulfillmentLocationId);
                throw new DomainException(PaymentsErrors.NoCapturedPayment, "The order has no captured or collected payment to refund.");
            }

            LocationGuard.EnsureAccess(actor, attempts[0].FulfillmentLocationId);
            var paid = attempts.FirstOrDefault(p => p.IsPaid && !p.IsExcess)
                ?? throw new DomainException(PaymentsErrors.NoCapturedPayment, "The order has no captured or collected payment to refund.");
            var draft = new RefundDraft(request.AmountPaise, request.ReasonCode, request.Method, requestedBy, request.ComplaintId, request.UpiId, request.Notes, null);
            var supportsRefunds = providers.CapabilitiesOf(paid.Provider).HasFlag(ProviderCapabilities.ProviderRefunds);
            var refund = paid.RequestRefund(draft, supportsRefunds, clock.UtcNow);
            await session.SaveChangesAsync(cancellationToken);
            return refund.ToResponse(paid);
        });
    }

    public Task<RefundResponse> ApproveAsync(Guid refundId, uint expectedVersion, CancellationToken cancellationToken)
        => ChangeAsync(refundId, expectedVersion, (payment, user, now) => payment.ApproveRefund(refundId, user, now), cancellationToken);

    public Task<RefundResponse> RejectAsync(Guid refundId, uint expectedVersion, RejectRefundRequest request, CancellationToken cancellationToken)
        => ChangeAsync(refundId, expectedVersion, (payment, user, _) => payment.RejectRefund(refundId, user, request?.Reason ?? string.Empty), cancellationToken);

    public Task<RefundResponse> MarkPaidAsync(Guid refundId, uint expectedVersion, MarkRefundPaidRequest request, CancellationToken cancellationToken)
        => ChangeAsync(refundId, expectedVersion, (payment, user, now) => payment.MarkRefundPaid(refundId, request?.Reference ?? string.Empty, user, now), cancellationToken);

    public async Task<RefundResponse> GetAsync(Guid refundId, CancellationToken cancellationToken)
    {
        var payment = await LoadAsync(refundId, cancellationToken);
        return payment.GetRefund(refundId).ToResponse(payment);
    }

    private async Task<RefundResponse> ChangeAsync(Guid refundId, uint expectedVersion, Func<Payment, Guid, DateTimeOffset, Refund> change, CancellationToken cancellationToken)
    {
        var user = LocationGuard.RequireUser(actor);
        var payment = await LoadAsync(refundId, cancellationToken);
        session.SetExpectedVersion(payment, expectedVersion);
        var refund = change(payment, user, clock.UtcNow);
        await session.SaveChangesAsync(cancellationToken);
        return refund.ToResponse(payment);
    }

    private async Task<Payment> LoadAsync(Guid refundId, CancellationToken cancellationToken)
    {
        var payment = await payments.GetByRefundIdAsync(refundId, cancellationToken)
            ?? throw new NotFoundException("REFUND_NOT_FOUND", "No refund has this id.");
        LocationGuard.EnsureAccess(actor, payment.FulfillmentLocationId);
        return payment;
    }
}
