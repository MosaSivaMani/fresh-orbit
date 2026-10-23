using FreshOrbit.Promotions.Application.Abstractions;
using FreshOrbit.Promotions.Application.Coupons;
using FreshOrbit.Promotions.Public;
using FreshOrbit.SharedKernel.Codes;

namespace FreshOrbit.Promotions.Infrastructure.Public;

/// <summary>Implementation of <see cref="IPromotionsApi"/> on the shared DbContext.</summary>
internal sealed class PromotionsApi(CouponRedemptionService redemptionService, IPromotionsReads reads) : IPromotionsApi
{
    public Task<CouponEvaluation> EvaluateAsync(EvaluateCouponRequest request, CancellationToken cancellationToken = default)
        => redemptionService.EvaluateAsync(request, cancellationToken);

    public Task<CouponRedemptionResult> RedeemAsync(RedeemCouponRequest request, CancellationToken cancellationToken = default)
        => redemptionService.RedeemAsync(request, cancellationToken);

    public async Task<IReadOnlyList<BannerInfo>> GetActiveBannersAsync(Guid? cityId, DateTimeOffset at, CancellationToken cancellationToken = default)
        => [.. (await reads.ListShownPromotionsAsync(cityId, at, cancellationToken))
            .Select(p => new BannerInfo(p.Id, p.Title, p.Subtitle, p.ImageKey, p.LinkType.ToCode(), p.LinkTarget, p.StartsAt, p.EndsAt, p.SortOrder))];

    public async Task<IReadOnlyList<VisibleCoupon>> ListVisibleCouponsAsync(Guid? cityId, Guid? customerId, DateTimeOffset at, CancellationToken cancellationToken = default)
        => [.. (await reads.ListVisibleCouponsAsync(cityId, customerId, at, cancellationToken))
            .Select(c => new VisibleCoupon(c.Code, c.Description, c.DiscountType.ToCode(), c.DiscountValue, c.MaxDiscountPaise, c.MinOrderPaise, c.ValidTo, c.FirstOrderOnly))];
}

/// <summary>
/// Stand-in until <c>Catalog.Public</c> exposes category ancestry and id existence (merge gap): a category's lineage
/// is the category itself (no sub-category matching) and every id is taken to exist.
/// </summary>
internal sealed class CatalogGapPromotionsCatalog : IPromotionsCatalog
{
    public Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetCategoryLineagesAsync(IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>(categoryIds.Distinct().ToDictionary(id => id, id => (IReadOnlyCollection<Guid>)[id]));

    public Task<IReadOnlyCollection<Guid>> FindMissingAsync(IReadOnlyCollection<Guid> categoryIds, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<Guid>>([]);
}

/// <summary>Stand-in until <c>Locations.Public</c> exposes a city lookup (merge gap): every city id is taken to exist.</summary>
internal sealed class LocationsGapCityDirectory : ICityDirectory
{
    public Task<bool> CityExistsAsync(Guid cityId, CancellationToken cancellationToken) => Task.FromResult(true);
}
