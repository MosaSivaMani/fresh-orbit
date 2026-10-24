using System.Globalization;
using FreshOrbit.Promotions.Domain.Banners;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.Promotions.Domain.Redemptions;

namespace FreshOrbit.Promotions.Application.Coupons;

/// <summary>Body of <c>POST /admin/coupons</c> and <c>PUT /admin/coupons/{id}</c> (fields = table columns, camelCase).</summary>
public sealed record CouponRequest(
    string Code,
    string? Description,
    DiscountType DiscountType,
    long DiscountValue,
    long? MaxDiscountPaise,
    long MinOrderPaise,
    Guid? CityId,
    bool FirstOrderOnly,
    int? PerCustomerLimit,
    int? TotalLimit,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    bool IsListed,
    IReadOnlyList<CouponScopeDto>? Scopes)
{
    public CouponDefinition ToDefinition() => new(
        Code,
        Description,
        DiscountType,
        DiscountValue,
        MaxDiscountPaise,
        MinOrderPaise,
        CityId,
        FirstOrderOnly,
        PerCustomerLimit,
        TotalLimit,
        ValidFrom,
        ValidTo,
        IsListed,
        [.. (Scopes ?? []).Select(s => new CouponScopeDefinition(s.ScopeType, s.ScopeId))]);
}

public sealed record CouponScopeDto(CouponScopeType ScopeType, Guid ScopeId);

public sealed record CouponResponse(
    Guid Id,
    string Code,
    string? Description,
    DiscountType DiscountType,
    long DiscountValue,
    long? MaxDiscountPaise,
    long MinOrderPaise,
    Guid? CityId,
    bool FirstOrderOnly,
    int? PerCustomerLimit,
    int? TotalLimit,
    int UsedCount,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    CouponStatus Status,
    bool IsListed,
    IReadOnlyList<CouponScopeDto> Scopes,
    string Version)
{
    public static CouponResponse From(Coupon coupon)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        return new CouponResponse(
            coupon.Id,
            coupon.Code,
            coupon.Description,
            coupon.DiscountType,
            coupon.DiscountValue,
            coupon.MaxDiscountPaise,
            coupon.MinOrderPaise,
            coupon.CityId,
            coupon.FirstOrderOnly,
            coupon.PerCustomerLimit,
            coupon.TotalLimit,
            coupon.UsedCount,
            coupon.ValidFrom,
            coupon.ValidTo,
            coupon.Status,
            coupon.IsListed,
            [.. coupon.Scopes.Select(s => new CouponScopeDto(s.ScopeType, s.ScopeId))],
            coupon.Version.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>One redemption; <c>OrderNumber</c> is read from Ordering (G16), one batch per page, null if the order is gone.</summary>
public sealed record RedemptionResponse(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    long DiscountPaise,
    RedemptionStatus Status,
    DateTimeOffset RedeemedAt,
    DateTimeOffset? ReversedAt,
    ReversalReason? ReversalReason)
{
    public string? OrderNumber { get; init; }
}

/// <summary>Filters of <c>GET /admin/coupons</c>.</summary>
public sealed record CouponListQuery(IReadOnlyList<CouponStatus>? Status, Guid? CityId, string? Q, int Page, int PageSize);

/// <summary>Body of <c>POST/PUT /admin/promotions</c>.</summary>
public sealed record PromotionRequest(
    string Title,
    string? Subtitle,
    string? ImageKey,
    PromotionLinkType LinkType,
    string LinkTarget,
    Guid? CityId,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    int SortOrder,
    bool IsActive)
{
    public PromotionDefinition ToDefinition() => new(Title, Subtitle, ImageKey, LinkType, LinkTarget, CityId, StartsAt, EndsAt, SortOrder, IsActive);
}

public sealed record PromotionResponse(
    Guid Id,
    string Title,
    string? Subtitle,
    string? ImageKey,
    PromotionLinkType LinkType,
    string LinkTarget,
    Guid? CityId,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    int SortOrder,
    bool IsActive,
    string Version)
{
    public static PromotionResponse From(Promotion promotion)
    {
        ArgumentNullException.ThrowIfNull(promotion);
        return new PromotionResponse(
            promotion.Id,
            promotion.Title,
            promotion.Subtitle,
            promotion.ImageKey,
            promotion.LinkType,
            promotion.LinkTarget,
            promotion.CityId,
            promotion.StartsAt,
            promotion.EndsAt,
            promotion.SortOrder,
            promotion.IsActive,
            promotion.Version.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>Read side of the admin and customer lists (implemented with no-tracking queries in Infrastructure).</summary>
public interface IPromotionsReads
{
    Task<(IReadOnlyList<CouponResponse> Items, int TotalCount)> ListCouponsAsync(CouponListQuery query, CancellationToken cancellationToken);

    Task<(IReadOnlyList<RedemptionResponse> Items, int TotalCount)> ListRedemptionsAsync(Guid couponId, int page, int pageSize, CancellationToken cancellationToken);

    Task<(IReadOnlyList<PromotionResponse> Items, int TotalCount)> ListPromotionsAsync(Guid? cityId, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<Promotion>> ListShownPromotionsAsync(Guid? cityId, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Listed, active, current coupons for the city that the customer has not used up (PROMO-R24).</summary>
    Task<IReadOnlyList<Coupon>> ListVisibleCouponsAsync(Guid? cityId, Guid? customerId, DateTimeOffset at, CancellationToken cancellationToken);
}
