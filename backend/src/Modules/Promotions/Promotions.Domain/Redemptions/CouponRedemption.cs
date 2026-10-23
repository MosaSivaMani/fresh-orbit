using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.Promotions.Domain.Coupons.Events;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Promotions.Domain.Redemptions;

/// <summary>Stored as APPLIED / REVERSED.</summary>
public enum RedemptionStatus
{
    Applied,
    Reversed,
}

/// <summary>Why a redemption was reversed. Stored as ORDER_CANCELLED / PAYMENT_FAILED.</summary>
public enum ReversalReason
{
    OrderCancelled,
    PaymentFailed,
}

/// <summary>
/// One use of a coupon by one order (<c>promotions.coupon_redemptions</c>, unique <c>order_id</c>). Created in the
/// place-order transaction; reversed by Promotions' handler of OrderCancelled / OrderPaymentFailed (PROMO-R18).
/// </summary>
public sealed class CouponRedemption : AggregateRoot
{
    private CouponRedemption()
    {
    }

    private CouponRedemption(Guid couponId, Guid customerId, Guid orderId, long discountPaise, DateTimeOffset redeemedAt)
    {
        CouponId = couponId;
        CustomerId = customerId;
        OrderId = orderId;
        DiscountPaise = discountPaise;
        Status = RedemptionStatus.Applied;
        RedeemedAt = redeemedAt;
    }

    public Guid CouponId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid OrderId { get; private set; }

    /// <summary>Discount frozen at redeem (PROMO-R15).</summary>
    public long DiscountPaise { get; private set; }

    public RedemptionStatus Status { get; private set; }

    public DateTimeOffset RedeemedAt { get; private set; }

    public DateTimeOffset? ReversedAt { get; private set; }

    public ReversalReason? ReversalReason { get; private set; }

    /// <summary>Redeems <paramref name="coupon"/> for an order: one more use on the coupon and a new APPLIED redemption.</summary>
    public static CouponRedemption Redeem(Coupon coupon, Guid customerId, Guid orderId, long discountPaise, bool waivesDeliveryFee, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        coupon.RecordUse();
        var redemption = new CouponRedemption(coupon.Id, customerId, orderId, discountPaise, now);
        redemption.Raise(new CouponRedeemed(coupon.Id, coupon.Code, redemption.Id, orderId, customerId, discountPaise, waivesDeliveryFee, now));
        return redemption;
    }

    /// <summary>Reverses an APPLIED redemption and gives the use back to the coupon. False when already reversed (no-op).</summary>
    public bool Reverse(Coupon coupon, ReversalReason reason, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        if (Status == RedemptionStatus.Reversed)
        {
            return false;
        }

        Status = RedemptionStatus.Reversed;
        ReversedAt = now;
        ReversalReason = reason;
        coupon.ReleaseUse();
        Raise(new CouponRedemptionReversed(CouponId, Id, OrderId, CustomerId, reason.ToCode(), now));
        return true;
    }
}
