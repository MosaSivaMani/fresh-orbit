using FreshOrbit.Promotions.Domain.Banners;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.Promotions.Domain.Redemptions;

namespace FreshOrbit.Promotions.Application.Abstractions;

public interface ICouponRepository
{
    /// <summary>The coupon with its scopes by normalized code (case-insensitive). Tracked when <paramref name="track"/>.</summary>
    Task<Coupon?> FindByCodeAsync(string code, bool track, CancellationToken cancellationToken);

    Task<Coupon?> FindByIdAsync(Guid couponId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, Guid? exceptCouponId, CancellationToken cancellationToken);

    Task<bool> HasRedemptionsAsync(Guid couponId, CancellationToken cancellationToken);

    /// <summary>APPLIED redemptions of the coupon by the customer (PROMO-R9).</summary>
    Task<int> CountAppliedAsync(Guid couponId, Guid customerId, CancellationToken cancellationToken);

    /// <summary>Up to <paramref name="take"/> ACTIVE / PAUSED coupons whose <c>valid_to</c> has passed (tracked).</summary>
    Task<IReadOnlyList<Coupon>> ListDueForExpiryAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);

    void Add(Coupon coupon);
}

public interface IRedemptionRepository
{
    /// <summary>The order's redemption (tracked), or null.</summary>
    Task<CouponRedemption?> FindByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    void Add(CouponRedemption redemption);
}

public interface IPromotionRepository
{
    Task<Promotion?> FindByIdAsync(Guid promotionId, CancellationToken cancellationToken);

    void Add(Promotion promotion);
}

/// <summary>
/// Catalog facts Promotions needs that <c>Catalog.Public</c> does not expose yet (merge gap): category ancestry for
/// category scopes (PROMO-R6) and existence of scope ids (PROMO-R22).
/// </summary>
public interface IPromotionsCatalog
{
    /// <summary>For each category id: the category itself and all its ancestors.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetCategoryLineagesAsync(IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken);

    /// <summary>The ids among the given categories and products that do not exist.</summary>
    Task<IReadOnlyCollection<Guid>> FindMissingAsync(IReadOnlyCollection<Guid> categoryIds, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
}

/// <summary>City existence check (PROMO-R22); <c>Locations.Public</c> has no city lookup yet (merge gap).</summary>
public interface ICityDirectory
{
    Task<bool> CityExistsAsync(Guid cityId, CancellationToken cancellationToken);
}
