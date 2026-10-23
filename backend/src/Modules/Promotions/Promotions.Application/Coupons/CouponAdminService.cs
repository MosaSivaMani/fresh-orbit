using FreshOrbit.Promotions.Application.Abstractions;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.Promotions.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Promotions.Application.Coupons;

/// <summary>Admin coupon commands (PROMO-R19..R23). One SaveChanges per command.</summary>
public sealed class CouponAdminService(
    ICouponRepository coupons,
    IPromotionsCatalog catalog,
    ICityDirectory cities,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    IClock clock)
{
    public async Task<CouponResponse> CreateAsync(CouponRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = request.ToDefinition();
        var coupon = Coupon.Create(definition);
        await EnsureReferencesAsync(coupon.Code, null, definition, cancellationToken);
        coupons.Add(coupon);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CouponResponse.From(coupon);
    }

    public async Task<CouponResponse> GetAsync(Guid couponId, CancellationToken cancellationToken)
        => CouponResponse.From(await LoadAsync(couponId, cancellationToken));

    public async Task<CouponResponse> UpdateAsync(Guid couponId, uint expectedVersion, CouponRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var coupon = await LoadAsync(couponId, cancellationToken);
        unitOfWork.SetExpectedVersion(coupon, expectedVersion);
        var definition = request.ToDefinition();
        var hasRedemptions = await coupons.HasRedemptionsAsync(coupon.Id, cancellationToken);
        coupon.Update(definition, hasRedemptions);
        await EnsureReferencesAsync(coupon.Code, coupon.Id, definition, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CouponResponse.From(coupon);
    }

    public async Task<CouponResponse> PauseAsync(Guid couponId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var coupon = await LoadAsync(couponId, cancellationToken);
        unitOfWork.SetExpectedVersion(coupon, expectedVersion);
        coupon.Pause(actor.UserId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CouponResponse.From(coupon);
    }

    public async Task<CouponResponse> ResumeAsync(Guid couponId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var coupon = await LoadAsync(couponId, cancellationToken);
        unitOfWork.SetExpectedVersion(coupon, expectedVersion);
        coupon.Resume(actor.UserId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CouponResponse.From(coupon);
    }

    private async Task<Coupon> LoadAsync(Guid couponId, CancellationToken cancellationToken)
        => await coupons.FindByIdAsync(couponId, cancellationToken)
            ?? throw new NotFoundException(PromotionsErrors.CouponNotFound, "No coupon has this id.");

    private async Task EnsureReferencesAsync(string code, Guid? exceptCouponId, CouponDefinition definition, CancellationToken cancellationToken)
    {
        if (await coupons.CodeExistsAsync(code, exceptCouponId, cancellationToken))
        {
            throw new ConflictException(PromotionsErrors.Duplicate, "This coupon code is already used; choose another code.", "code");
        }

        var errors = new List<FieldError>();
        if (definition.CityId is { } cityId && !await cities.CityExistsAsync(cityId, cancellationToken))
        {
            errors.Add(new FieldError("cityId", PromotionsErrors.ValidationFailed, "City does not exist."));
        }

        if (definition.Scopes.Count > 0)
        {
            var missing = await catalog.FindMissingAsync(
                [.. definition.Scopes.Where(s => s.ScopeType == CouponScopeType.Category).Select(s => s.ScopeId)],
                [.. definition.Scopes.Where(s => s.ScopeType == CouponScopeType.Product).Select(s => s.ScopeId)],
                cancellationToken);
            for (var i = 0; i < definition.Scopes.Count; i++)
            {
                if (missing.Contains(definition.Scopes[i].ScopeId))
                {
                    errors.Add(new FieldError($"scopes[{i}].scopeId", PromotionsErrors.ValidationFailed, "Scope id does not exist."));
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }
    }
}
