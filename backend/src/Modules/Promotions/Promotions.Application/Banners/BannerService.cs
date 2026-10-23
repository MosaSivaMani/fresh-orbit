using FreshOrbit.Promotions.Application.Abstractions;
using FreshOrbit.Promotions.Application.Coupons;
using FreshOrbit.Promotions.Domain.Banners;
using FreshOrbit.Promotions.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Promotions.Application.Banners;

/// <summary>Admin home banners (PROMO-R25). Link targets must exist.</summary>
public sealed class BannerService(
    IPromotionRepository promotions,
    ICouponRepository coupons,
    IPromotionsCatalog catalog,
    ICityDirectory cities,
    IUnitOfWork unitOfWork)
{
    public async Task<PromotionResponse> CreateAsync(PromotionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var promotion = Promotion.Create(request.ToDefinition());
        await EnsureReferencesAsync(promotion, cancellationToken);
        promotions.Add(promotion);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return PromotionResponse.From(promotion);
    }

    public async Task<PromotionResponse> GetAsync(Guid promotionId, CancellationToken cancellationToken)
        => PromotionResponse.From(await LoadAsync(promotionId, cancellationToken));

    public async Task<PromotionResponse> UpdateAsync(Guid promotionId, uint expectedVersion, PromotionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var promotion = await LoadAsync(promotionId, cancellationToken);
        unitOfWork.SetExpectedVersion(promotion, expectedVersion);
        promotion.Update(request.ToDefinition());
        await EnsureReferencesAsync(promotion, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return PromotionResponse.From(promotion);
    }

    private async Task<Promotion> LoadAsync(Guid promotionId, CancellationToken cancellationToken)
        => await promotions.FindByIdAsync(promotionId, cancellationToken)
            ?? throw new NotFoundException(PromotionsErrors.PromotionNotFound, "No promotion has this id.");

    private async Task EnsureReferencesAsync(Promotion promotion, CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();
        if (promotion.CityId is { } cityId && !await cities.CityExistsAsync(cityId, cancellationToken))
        {
            errors.Add(new FieldError("cityId", PromotionsErrors.ValidationFailed, "City does not exist."));
        }

        var exists = promotion.LinkType switch
        {
            PromotionLinkType.Coupon => await coupons.FindByCodeAsync(promotion.LinkTarget, track: false, cancellationToken) is not null,
            PromotionLinkType.Category => (await catalog.FindMissingAsync([Guid.Parse(promotion.LinkTarget)], [], cancellationToken)).Count == 0,
            _ => (await catalog.FindMissingAsync([], [Guid.Parse(promotion.LinkTarget)], cancellationToken)).Count == 0,
        };
        if (!exists)
        {
            errors.Add(new FieldError("linkTarget", PromotionsErrors.ValidationFailed, "Link target does not exist."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }
    }
}
