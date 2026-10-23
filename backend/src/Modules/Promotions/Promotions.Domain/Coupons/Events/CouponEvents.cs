using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Promotions.Domain.Coupons.Events;

/// <summary>A coupon changed status (pause, resume, expiry). Codes are ACTIVE / PAUSED / EXPIRED.</summary>
public sealed record CouponStatusChanged(Guid CouponId, string Code, string From, string To, Guid? ActorId, DateTimeOffset At) : DomainEvent;

/// <summary>A coupon was redeemed inside the place-order transaction.</summary>
public sealed record CouponRedeemed(
    Guid CouponId,
    string Code,
    Guid RedemptionId,
    Guid OrderId,
    Guid CustomerId,
    long DiscountPaise,
    bool WaivesDeliveryFee,
    DateTimeOffset RedeemedAt) : DomainEvent;

/// <summary>A redemption was reversed (order cancelled or payment failed). Reason ORDER_CANCELLED / PAYMENT_FAILED.</summary>
public sealed record CouponRedemptionReversed(
    Guid CouponId,
    Guid RedemptionId,
    Guid OrderId,
    Guid CustomerId,
    string Reason,
    DateTimeOffset ReversedAt) : DomainEvent;
