namespace FreshOrbit.Promotions.Domain.Coupons;

/// <summary>Stored as PERCENT / FLAT / FREE_DELIVERY. PERCENT values are basis points, FLAT values paise.</summary>
public enum DiscountType
{
    Percent,
    Flat,
    FreeDelivery,
}

/// <summary>Stored as ACTIVE / PAUSED / EXPIRED. "Not started" and "limit reached" are computed, never stored.</summary>
public enum CouponStatus
{
    Active,
    Paused,
    Expired,
}

/// <summary>Stored as CATEGORY / PRODUCT.</summary>
public enum CouponScopeType
{
    Category,
    Product,
}

/// <summary>Result of evaluating a coupon (PROMO-R12): APPLIED, NOT_APPLICABLE (fixable by a cart change) or INVALID.</summary>
public enum CouponOutcomeStatus
{
    Applied,
    NotApplicable,
    Invalid,
}
