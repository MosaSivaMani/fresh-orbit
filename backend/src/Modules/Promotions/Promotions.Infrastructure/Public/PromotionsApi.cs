using FreshOrbit.Promotions.Application.Coupons;
using FreshOrbit.Promotions.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Storage;

namespace FreshOrbit.Promotions.Infrastructure.Public;

/// <summary>Implementation of <see cref="IPromotionsApi"/> on the shared DbContext.</summary>
internal sealed class PromotionsApi(CouponRedemptionService redemptionService, IPromotionsReads reads, IFileStorage storage) : IPromotionsApi
{
    public Task<CouponEvaluation> EvaluateAsync(EvaluateCouponRequest request, CancellationToken cancellationToken = default)
        => redemptionService.EvaluateAsync(request, cancellationToken);

    public Task<CouponRedemptionResult> RedeemAsync(RedeemCouponRequest request, CancellationToken cancellationToken = default)
        => redemptionService.RedeemAsync(request, cancellationToken);

    public async Task<IReadOnlyList<BannerInfo>> GetActiveBannersAsync(Guid? cityId, DateTimeOffset at, CancellationToken cancellationToken = default)
        => [.. (await reads.ListShownPromotionsAsync(cityId, at, cancellationToken))
            .Select(p => new BannerInfo(p.Id, p.Title, p.Subtitle, p.ImageKey, p.LinkType.ToCode(), p.LinkTarget, p.StartsAt, p.EndsAt, p.SortOrder)
            {
                ImageUrl = ImageUrl(p.ImageKey),
            })];

    public async Task<IReadOnlyList<VisibleCoupon>> ListVisibleCouponsAsync(Guid? cityId, Guid? customerId, DateTimeOffset at, CancellationToken cancellationToken = default)
        => [.. (await reads.ListVisibleCouponsAsync(cityId, customerId, at, cancellationToken))
            .Select(c => new VisibleCoupon(c.Code, c.Description, c.DiscountType.ToCode(), c.DiscountValue, c.MaxDiscountPaise, c.MinOrderPaise, c.ValidTo, c.FirstOrderOnly))];

    /// <summary>The storage's public URL of a valid key; null without one (W11).</summary>
    private string? ImageUrl(string? key) => key is not null && StorageKeys.IsValid(key) ? storage.GetPublicUrl(key) : null;
}
