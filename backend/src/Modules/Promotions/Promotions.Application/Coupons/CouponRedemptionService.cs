using FreshOrbit.Promotions.Application.Abstractions;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.Promotions.Domain.Redemptions;
using FreshOrbit.Promotions.Domain.Shared;
using FreshOrbit.Promotions.Public;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Promotions.Application.Coupons;

/// <summary>
/// Evaluate (read-only, cart and preview) and Redeem (checkout transaction) of a coupon (PROMO-R1..R16).
/// Reads: the coupon with scopes, category lineages (one batched call) and, when the coupon has a per-customer
/// limit, the customer's APPLIED count. Redeem tracks the coupon and a new redemption; the caller saves.
/// </summary>
public sealed class CouponRedemptionService(
    ICouponRepository coupons,
    IRedemptionRepository redemptions,
    IPromotionsCatalog catalog,
    IClock clock)
{
    public async Task<CouponEvaluation> EvaluateAsync(EvaluateCouponRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = Coupon.NormalizeCode(request.Code);
        var coupon = await coupons.FindByCodeAsync(code, track: false, cancellationToken);
        if (coupon is null)
        {
            return new CouponEvaluation(code, CouponApplicationStatus.Invalid, PromotionsErrors.CouponInvalid, 0, false, []);
        }

        var outcome = await EvaluateAsync(coupon, request.CustomerId, request.CityId, request.HasQualifyingOrder, request.At, request.Lines, cancellationToken);
        return ToEvaluation(coupon.Code, outcome);
    }

    public async Task<CouponRedemptionResult> RedeemAsync(RedeemCouponRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var coupon = await coupons.FindByCodeAsync(Coupon.NormalizeCode(request.Code), track: true, cancellationToken)
            ?? throw new DomainException(PromotionsErrors.CouponInvalid, "This coupon code is not valid; remove it to continue.", "couponCode");

        var outcome = await EvaluateAsync(coupon, request.CustomerId, request.CityId, request.HasQualifyingOrder, request.At, request.Lines, cancellationToken);
        if (outcome.Status != CouponOutcomeStatus.Applied)
        {
            throw new DomainException(outcome.Code!, RefusalMessage(outcome.Code!), "couponCode");
        }

        var redemption = CouponRedemption.Redeem(coupon, request.CustomerId, request.OrderId, outcome.DiscountPaise, outcome.WaivesDeliveryFee, clock.UtcNow);
        redemptions.Add(redemption);
        var evaluation = ToEvaluation(coupon.Code, outcome);
        return new CouponRedemptionResult(redemption.Id, coupon.Id, coupon.Code, outcome.DiscountPaise, outcome.WaivesDeliveryFee, evaluation.Allocations);
    }

    /// <summary>One sentence per COUPON_* code naming the field to fix.</summary>
    public static string RefusalMessage(string code) => code switch
    {
        PromotionsErrors.CouponExpired => "This coupon has expired; remove it to continue.",
        PromotionsErrors.CouponNotStarted => "This coupon is not valid yet; remove it to continue.",
        PromotionsErrors.CouponLimitReached => "This coupon has reached its usage limit; remove it to continue.",
        PromotionsErrors.CouponFirstOrderOnly => "This coupon is for a first order only; remove it to continue.",
        PromotionsErrors.CouponMinOrderNotMet => "Add more eligible items to use this coupon, or remove the coupon.",
        _ => "This coupon cannot be used for this cart; remove it to continue.",
    };

    private async Task<CouponOutcome> EvaluateAsync(
        Coupon coupon,
        Guid? customerId,
        Guid cityId,
        bool hasQualifyingOrder,
        DateTimeOffset at,
        IReadOnlyList<CouponLine> lines,
        CancellationToken cancellationToken)
    {
        var needsLineage = coupon.Scopes.Any(s => s.ScopeType == CouponScopeType.Category);
        var lineages = needsLineage
            ? await catalog.GetCategoryLineagesAsync([.. lines.Select(l => l.CategoryId).Distinct()], cancellationToken)
            : new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        var applied = coupon.PerCustomerLimit is not null && customerId is { } customer
            ? await coupons.CountAppliedAsync(coupon.Id, customer, cancellationToken)
            : 0;

        var inputs = lines
            .Select(l => new CouponLineInput(l.LineRef, l.ProductId, lineages.TryGetValue(l.CategoryId, out var lineage) ? lineage : [l.CategoryId], l.AmountPaise))
            .ToList();
        return coupon.Evaluate(new CouponEvaluationContext(at, cityId, hasQualifyingOrder, applied, inputs));
    }

    private static CouponEvaluation ToEvaluation(string code, CouponOutcome outcome) => new(
        code,
        outcome.Status switch
        {
            CouponOutcomeStatus.Applied => CouponApplicationStatus.Applied,
            CouponOutcomeStatus.NotApplicable => CouponApplicationStatus.NotApplicable,
            _ => CouponApplicationStatus.Invalid,
        },
        outcome.Code,
        outcome.DiscountPaise,
        outcome.WaivesDeliveryFee,
        [.. outcome.Allocations.Select(a => new CouponLineDiscount(a.LineRef, a.DiscountPaise))]);
}
