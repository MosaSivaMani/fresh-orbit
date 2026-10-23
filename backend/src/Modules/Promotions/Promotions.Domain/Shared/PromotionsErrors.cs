namespace FreshOrbit.Promotions.Domain.Shared;

/// <summary>Error codes raised by the Promotions module (docs/api §2 Promotions + common codes).</summary>
public static class PromotionsErrors
{
    public const string CouponInvalid = "COUPON_INVALID";
    public const string CouponExpired = "COUPON_EXPIRED";
    public const string CouponNotStarted = "COUPON_NOT_STARTED";
    public const string CouponLimitReached = "COUPON_LIMIT_REACHED";
    public const string CouponFirstOrderOnly = "COUPON_FIRST_ORDER_ONLY";
    public const string CouponMinOrderNotMet = "COUPON_MIN_ORDER_NOT_MET";
    public const string CouponNotFound = "COUPON_NOT_FOUND";
    public const string PromotionNotFound = "PROMOTION_NOT_FOUND";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Duplicate = "DUPLICATE";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
}
