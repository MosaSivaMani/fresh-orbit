using FreshOrbit.Promotions.Application.Abstractions;
using FreshOrbit.Promotions.Domain.Redemptions;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Promotions.Application.Handlers;

/// <summary>
/// Reverses an order's coupon redemption (PROMO-R18). Body of Promotions' handlers of Ordering's
/// <c>OrderCancelled</c> (reason ORDER_CANCELLED) and <c>OrderPaymentFailed</c> (reason PAYMENT_FAILED). Tracks the
/// redemption and the coupon; the outbox dispatcher saves them with the inbox row (handlers never save). No
/// redemption, or already REVERSED → no-op. A lost race on the coupon row fails the save and the handler is retried.
/// </summary>
public sealed class RedemptionReversalService(IRedemptionRepository redemptions, ICouponRepository coupons, IClock clock)
{
    /// <returns>True when a redemption was reversed by this call.</returns>
    public async Task<bool> ReverseForOrderAsync(Guid orderId, ReversalReason reason, CancellationToken cancellationToken)
    {
        var redemption = await redemptions.FindByOrderAsync(orderId, cancellationToken);
        if (redemption is null || redemption.Status == RedemptionStatus.Reversed)
        {
            return false;
        }

        var coupon = await coupons.FindByIdAsync(redemption.CouponId, cancellationToken)
            ?? throw new InvalidOperationException($"Coupon {redemption.CouponId} of redemption {redemption.Id} does not exist.");
        return redemption.Reverse(coupon, reason, clock.UtcNow);
    }
}
