namespace FreshOrbit.Promotions.Domain.Coupons;

/// <summary>Everything an admin sets on a coupon (create and update).</summary>
public sealed record CouponDefinition(
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
    IReadOnlyList<CouponScopeDefinition> Scopes);

public sealed record CouponScopeDefinition(CouponScopeType ScopeType, Guid ScopeId);

/// <summary>
/// A coupon scope row (<c>promotions.coupon_scopes</c>). No scopes = every line is eligible.
/// </summary>
public sealed class CouponScope
{
    public CouponScope(CouponScopeType scopeType, Guid scopeId)
    {
        ScopeType = scopeType;
        ScopeId = scopeId;
    }

    public CouponScopeType ScopeType { get; private set; }

    public Guid ScopeId { get; private set; }
}

/// <summary>What a coupon is checked against (PROMO-R2..R10).</summary>
/// <param name="CustomerAppliedRedemptions">APPLIED redemptions of this coupon by the customer (0 for guests).</param>
public sealed record CouponEvaluationContext(
    DateTimeOffset At,
    Guid CityId,
    bool HasQualifyingOrder,
    int CustomerAppliedRedemptions,
    IReadOnlyList<CouponLineInput> Lines);

/// <param name="CategoryLineage">The product's category and all its ancestors (PROMO-R6 category scope includes descendants).</param>
/// <param name="AmountPaise">Item + preparation amount of a sellable line.</param>
public sealed record CouponLineInput(Guid LineRef, Guid ProductId, IReadOnlyCollection<Guid> CategoryLineage, long AmountPaise);

public sealed record LineDiscount(Guid LineRef, long DiscountPaise);

/// <summary>Outcome of an evaluation. <see cref="Code"/> is the COUPON_* reason when not applied.</summary>
public sealed record CouponOutcome(
    CouponOutcomeStatus Status,
    string? Code,
    long DiscountPaise,
    bool WaivesDeliveryFee,
    long EligibleSubtotalPaise,
    IReadOnlyList<LineDiscount> Allocations)
{
    public static CouponOutcome Refused(CouponOutcomeStatus status, string code) => new(status, code, 0, false, 0, []);
}
