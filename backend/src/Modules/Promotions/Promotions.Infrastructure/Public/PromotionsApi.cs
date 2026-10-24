using FreshOrbit.Promotions.Application.Abstractions;
using FreshOrbit.Promotions.Application.Coupons;
using FreshOrbit.Promotions.Domain.Banners;
using FreshOrbit.Promotions.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Storage;

namespace FreshOrbit.Promotions.Infrastructure.Public;

/// <summary>Implementation of <see cref="IPromotionsApi"/> on the shared DbContext.</summary>
internal sealed class PromotionsApi(CouponRedemptionService redemptionService, IPromotionsReads reads, IPromotionsCatalog catalog, IFileStorage storage) : IPromotionsApi
{
    public Task<CouponEvaluation> EvaluateAsync(EvaluateCouponRequest request, CancellationToken cancellationToken = default)
        => redemptionService.EvaluateAsync(request, cancellationToken);

    public Task<CouponRedemptionResult> RedeemAsync(RedeemCouponRequest request, CancellationToken cancellationToken = default)
        => redemptionService.RedeemAsync(request, cancellationToken);

    public async Task<IReadOnlyList<BannerInfo>> GetActiveBannersAsync(Guid? cityId, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var shown = await reads.ListShownPromotionsAsync(cityId, at, cancellationToken);
        var slugs = await catalog.GetLinkSlugsAsync(LinkIds(shown, PromotionLinkType.Category), LinkIds(shown, PromotionLinkType.Product), cancellationToken);
        return [.. shown.Select(p => new BannerInfo(p.Id, p.Title, p.Subtitle, p.ImageKey, p.LinkType.ToCode(), p.LinkTarget, p.StartsAt, p.EndsAt, p.SortOrder)
        {
            ImageUrl = ImageUrl(p.ImageKey),
            LinkSlug = p.LinkType is PromotionLinkType.Coupon || !Guid.TryParse(p.LinkTarget, out var id) ? null : slugs.GetValueOrDefault(id),
        })];
    }

    public async Task<IReadOnlyList<VisibleCoupon>> ListVisibleCouponsAsync(Guid? cityId, Guid? customerId, DateTimeOffset at, CancellationToken cancellationToken = default)
        => [.. (await reads.ListVisibleCouponsAsync(cityId, customerId, at, cancellationToken))
            .Select(c => new VisibleCoupon(c.Code, c.Description, c.DiscountType.ToCode(), c.DiscountValue, c.MaxDiscountPaise, c.MinOrderPaise, c.ValidTo, c.FirstOrderOnly))];

    /// <summary>The storage's public URL of a valid key; null without one (W11).</summary>
    private string? ImageUrl(string? key) => key is not null && StorageKeys.IsValid(key) ? storage.GetPublicUrl(key) : null;

    /// <summary>The ids of CATEGORY or PRODUCT link targets (stored as uuids, PROMO rules) of <paramref name="type"/>.</summary>
    private static List<Guid> LinkIds(IEnumerable<Promotion> promotions, PromotionLinkType type)
        => [.. promotions.Where(p => p.LinkType == type).Select(p => Guid.TryParse(p.LinkTarget, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct()];
}
