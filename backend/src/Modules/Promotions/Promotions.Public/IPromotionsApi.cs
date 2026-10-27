namespace FreshOrbit.Promotions.Public;

/// <summary>What other modules may ask the Promotions module (docs/product/promotions.md §1).</summary>
public interface IPromotionsApi
{
    /// <summary>
    /// Checks a coupon against cart lines and computes its discount and per-line allocation. Read-only; never throws
    /// for rule failures (PROMO-R12): the result carries <see cref="CouponApplicationStatus"/> and the COUPON_* code.
    /// </summary>
    Task<CouponEvaluation> EvaluateAsync(EvaluateCouponRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-checks the coupon, increments its use count and adds an APPLIED redemption for the order. Tracked on the
    /// shared DbContext and saved by the caller's single SaveChanges (checkout transaction); never saves. Refuses
    /// with a 422 DomainException carrying the COUPON_* code. A lost race on the coupon row surfaces as
    /// ConcurrentSaveException at the caller's save.
    /// </summary>
    Task<CouponRedemptionResult> RedeemAsync(RedeemCouponRequest request, CancellationToken cancellationToken = default);

    /// <summary>Home banners shown at <paramref name="at"/> for the city plus all-city banners, by sort order.</summary>
    Task<IReadOnlyList<BannerInfo>> GetActiveBannersAsync(Guid? cityId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>Listed coupons currently usable in the city and not exhausted for the customer (PROMO-R24).</summary>
    Task<IReadOnlyList<VisibleCoupon>> ListVisibleCouponsAsync(Guid? cityId, Guid? customerId, DateTimeOffset at, CancellationToken cancellationToken = default);
}

/// <summary>Stored / returned as APPLIED / NOT_APPLICABLE / INVALID.</summary>
public enum CouponApplicationStatus
{
    Applied,
    NotApplicable,
    Invalid,
}

/// <param name="Code">Coupon code as typed (matched case-insensitively).</param>
/// <param name="HasQualifyingOrder">The customer has an order not CANCELLED / PAYMENT_FAILED (from Ordering).</param>
/// <param name="Lines">Sellable lines only, in line order.</param>
public sealed record EvaluateCouponRequest(
    string Code,
    Guid? CustomerId,
    Guid CityId,
    bool HasQualifyingOrder,
    DateTimeOffset At,
    IReadOnlyList<CouponLine> Lines);

/// <param name="LineRef">Caller's line id, echoed in allocations.</param>
/// <param name="AmountPaise">Line item + preparation amount.</param>
public sealed record CouponLine(Guid LineRef, Guid ProductId, Guid CategoryId, long AmountPaise);

public sealed record CouponLineDiscount(Guid LineRef, long DiscountPaise);

/// <param name="Code">Normalized coupon code.</param>
/// <param name="RefusalCode">COUPON_* reason when not applied.</param>
/// <param name="Allocations">Discount per eligible line; sums to <paramref name="DiscountPaise"/>.</param>
public sealed record CouponEvaluation(
    string Code,
    CouponApplicationStatus Status,
    string? RefusalCode,
    long DiscountPaise,
    bool WaivesDeliveryFee,
    IReadOnlyList<CouponLineDiscount> Allocations);

public sealed record RedeemCouponRequest(
    string Code,
    Guid CustomerId,
    Guid OrderId,
    Guid CityId,
    bool HasQualifyingOrder,
    DateTimeOffset At,
    IReadOnlyList<CouponLine> Lines);

public sealed record CouponRedemptionResult(
    Guid RedemptionId,
    Guid CouponId,
    string Code,
    long DiscountPaise,
    bool WaivesDeliveryFee,
    IReadOnlyList<CouponLineDiscount> Allocations);

/// <param name="ImageKey">Storage key of the banner image (kept for existing clients; show <c>ImageUrl</c>).</param>
/// <param name="LinkType">CATEGORY / PRODUCT / COUPON.</param>
public sealed record BannerInfo(
    Guid Id,
    string Title,
    string? Subtitle,
    string? ImageKey,
    string LinkType,
    string LinkTarget,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    int SortOrder)
{
    /// <summary>Public URL of <see cref="ImageKey"/> resolved by the file storage (W11); null without an image.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>Short label the admin set for the banner ("NEW", "20% OFF"); null when none. Show it as given.</summary>
    public string? Badge { get; init; }

    /// <summary>
    /// The storefront slug of a CATEGORY or PRODUCT link target (W14), so clients link by slug; null for a
    /// COUPON link or when the target no longer exists. <see cref="LinkTarget"/> stays the id.
    /// </summary>
    public string? LinkSlug { get; init; }
}

/// <param name="DiscountType">PERCENT (value in bp) / FLAT (paise) / FREE_DELIVERY.</param>
public sealed record VisibleCoupon(
    string Code,
    string? Description,
    string DiscountType,
    long DiscountValue,
    long? MaxDiscountPaise,
    long MinOrderPaise,
    DateTimeOffset? ValidTo,
    bool FirstOrderOnly);
