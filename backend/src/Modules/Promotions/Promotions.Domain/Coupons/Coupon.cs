using System.Text.RegularExpressions;
using FreshOrbit.Promotions.Domain.Coupons.Events;
using FreshOrbit.Promotions.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Promotions.Domain.Coupons;

/// <summary>
/// A discount code (<c>promotions.coupons</c>, row version <c>xmin</c>). Decides validity (PROMO-R2..R10), the
/// discount and its allocation over lines (R11..R14) and <see cref="UsedCount"/>. Every redeem and reverse changes
/// <see cref="UsedCount"/>, so the row version serializes total and per-customer limit checks (R16).
/// </summary>
public sealed partial class Coupon : AggregateRoot
{
    public const int MaxDescriptionLength = 200;

    private readonly List<CouponScope> _scopes = [];

    private Coupon()
    {
        Code = string.Empty;
    }

    private Coupon(CouponDefinition definition)
    {
        Code = NormalizeCode(definition.Code);
        Status = CouponStatus.Active;
        Apply(definition);
    }

    public string Code { get; private set; }

    public string? Description { get; private set; }

    public DiscountType DiscountType { get; private set; }

    /// <summary>PERCENT: basis points (1–10000); FLAT: paise; FREE_DELIVERY: 0.</summary>
    public long DiscountValue { get; private set; }

    public long? MaxDiscountPaise { get; private set; }

    public long MinOrderPaise { get; private set; }

    public Guid? CityId { get; private set; }

    public bool FirstOrderOnly { get; private set; }

    public int? PerCustomerLimit { get; private set; }

    public int? TotalLimit { get; private set; }

    public int UsedCount { get; private set; }

    public DateTimeOffset ValidFrom { get; private set; }

    public DateTimeOffset? ValidTo { get; private set; }

    public CouponStatus Status { get; private set; }

    /// <summary>Shown in <c>GET /coupons</c> (PROMO-R24); private coupons are applied by code only.</summary>
    public bool IsListed { get; private set; }

    public IReadOnlyList<CouponScope> Scopes => _scopes;

    /// <summary>Trims and upper-cases a code (PROMO-R1).</summary>
    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Creates an ACTIVE coupon; refuses with field-keyed errors (PROMO-R19..R21).</summary>
    public static Coupon Create(CouponDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ThrowIfInvalid(Validate(definition, usedCount: 0));
        return new Coupon(definition);
    }

    /// <summary>
    /// Field errors of a definition: code format, discount value per type, limits, dates, first-order rule and
    /// total limit not below <paramref name="usedCount"/>.
    /// </summary>
    public static IReadOnlyList<FieldError> Validate(CouponDefinition definition, int usedCount)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<FieldError>();
        void Fail(string field, string message) => errors.Add(new FieldError(field, PromotionsErrors.ValidationFailed, message));

        if (!CodePattern().IsMatch(NormalizeCode(definition.Code)))
        {
            Fail("code", "Code must be 3 to 20 letters or digits.");
        }

        if (definition.Description is { Length: > MaxDescriptionLength })
        {
            Fail("description", $"Description must be at most {MaxDescriptionLength} characters.");
        }

        switch (definition.DiscountType)
        {
            case DiscountType.Percent:
                if (definition.DiscountValue is < 1 or > BasisPoints.Whole)
                {
                    Fail("discountValue", "Percent discount value must be between 1 and 10000 basis points.");
                }

                if (definition.MaxDiscountPaise is null)
                {
                    Fail("maxDiscountPaise", "Max discount is required for a percent coupon.");
                }

                break;
            case DiscountType.Flat:
                if (definition.DiscountValue <= 0)
                {
                    Fail("discountValue", "Flat discount value must be greater than 0 paise.");
                }

                break;
            case DiscountType.FreeDelivery:
                if (definition.DiscountValue != 0)
                {
                    Fail("discountValue", "Free delivery coupons must have discount value 0.");
                }

                break;
        }

        if (definition.MaxDiscountPaise is <= 0)
        {
            Fail("maxDiscountPaise", "Max discount must be greater than 0 paise.");
        }

        if (definition.MinOrderPaise < 0)
        {
            Fail("minOrderPaise", "Min order must not be negative.");
        }

        if (definition.PerCustomerLimit is < 1)
        {
            Fail("perCustomerLimit", "Per-customer limit must be at least 1, or empty for unlimited.");
        }

        if (definition.TotalLimit is < 1)
        {
            Fail("totalLimit", "Total limit must be at least 1, or empty for unlimited.");
        }
        else if (definition.TotalLimit is { } total && total < usedCount)
        {
            Fail("totalLimit", $"Total limit cannot be below the {usedCount} uses already made.");
        }

        if (definition.FirstOrderOnly && definition.PerCustomerLimit != 1)
        {
            Fail("perCustomerLimit", "First-order coupons must have a per-customer limit of 1.");
        }

        if (definition.ValidTo is { } validTo && validTo <= definition.ValidFrom)
        {
            Fail("validTo", "Valid to must be after valid from.");
        }

        return errors;
    }

    /// <summary>Updates the coupon (PROMO-R19..R21). The code cannot change once a redemption exists.</summary>
    public void Update(CouponDefinition definition, bool hasRedemptions)
    {
        ArgumentNullException.ThrowIfNull(definition);
        EnsureNotExpired();
        var errors = Validate(definition, UsedCount).ToList();
        var code = NormalizeCode(definition.Code);
        if (hasRedemptions && !string.Equals(code, Code, StringComparison.Ordinal))
        {
            errors.Add(new FieldError("code", PromotionsErrors.ValidationFailed, "Code cannot change once the coupon has been used."));
        }

        ThrowIfInvalid(errors);
        Code = code;
        Apply(definition);
    }

    public void Pause(Guid? actorId, DateTimeOffset now)
    {
        EnsureNotExpired();
        if (Status != CouponStatus.Active)
        {
            throw new ConflictException(PromotionsErrors.InvalidStatusTransition, "Only an active coupon can be paused.");
        }

        ChangeStatus(CouponStatus.Paused, actorId, now);
    }

    public void Resume(Guid? actorId, DateTimeOffset now)
    {
        EnsureNotExpired();
        if (Status != CouponStatus.Paused)
        {
            throw new ConflictException(PromotionsErrors.InvalidStatusTransition, "Only a paused coupon can be resumed.");
        }

        if (ValidTo is { } validTo && validTo <= now)
        {
            throw new ConflictException(PromotionsErrors.InvalidStatusTransition, "A coupon past its valid-to date cannot be resumed.");
        }

        ChangeStatus(CouponStatus.Active, actorId, now);
    }

    /// <summary>Marks an ACTIVE or PAUSED coupon whose window has ended as EXPIRED (job tidy-up). False when not due.</summary>
    public bool ExpireIfDue(DateTimeOffset now)
    {
        if (Status == CouponStatus.Expired || ValidTo is not { } validTo || validTo > now)
        {
            return false;
        }

        ChangeStatus(CouponStatus.Expired, null, now);
        return true;
    }

    /// <summary>Validity and discount for a cart or order (PROMO-R2..R14). Never throws for rule failures.</summary>
    public CouponOutcome Evaluate(CouponEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Status == CouponStatus.Expired)
        {
            return CouponOutcome.Refused(CouponOutcomeStatus.Invalid, PromotionsErrors.CouponExpired);
        }

        if (Status != CouponStatus.Active)
        {
            return CouponOutcome.Refused(CouponOutcomeStatus.Invalid, PromotionsErrors.CouponInvalid);
        }

        if (context.At < ValidFrom)
        {
            return CouponOutcome.Refused(CouponOutcomeStatus.Invalid, PromotionsErrors.CouponNotStarted);
        }

        if (ValidTo is { } validTo && context.At >= validTo)
        {
            return CouponOutcome.Refused(CouponOutcomeStatus.Invalid, PromotionsErrors.CouponExpired);
        }

        if (CityId is { } cityId && cityId != context.CityId)
        {
            return CouponOutcome.Refused(CouponOutcomeStatus.Invalid, PromotionsErrors.CouponInvalid);
        }

        var eligible = context.Lines.Where(IsEligible).ToList();
        if (eligible.Count == 0)
        {
            return CouponOutcome.Refused(CouponOutcomeStatus.Invalid, PromotionsErrors.CouponInvalid);
        }

        if (FirstOrderOnly && context.HasQualifyingOrder)
        {
            return CouponOutcome.Refused(CouponOutcomeStatus.Invalid, PromotionsErrors.CouponFirstOrderOnly);
        }

        if (TotalLimit is { } total && UsedCount >= total)
        {
            return CouponOutcome.Refused(CouponOutcomeStatus.Invalid, PromotionsErrors.CouponLimitReached);
        }

        if (PerCustomerLimit is { } perCustomer && context.CustomerAppliedRedemptions >= perCustomer)
        {
            return CouponOutcome.Refused(CouponOutcomeStatus.Invalid, PromotionsErrors.CouponLimitReached);
        }

        var eligibleSubtotal = Money.Sum(eligible.Select(l => l.AmountPaise));
        if (eligibleSubtotal < MinOrderPaise)
        {
            return CouponOutcome.Refused(CouponOutcomeStatus.NotApplicable, PromotionsErrors.CouponMinOrderNotMet);
        }

        var discount = DiscountFor(eligibleSubtotal);
        var parts = Money.Allocate(discount, [.. eligible.Select(l => l.AmountPaise)]);
        var allocations = eligible.Select((line, i) => new LineDiscount(line.LineRef, parts[i])).ToList();
        return new CouponOutcome(CouponOutcomeStatus.Applied, null, discount, DiscountType == DiscountType.FreeDelivery, eligibleSubtotal, allocations);
    }

    /// <summary>The discount on an eligible subtotal (PROMO-R11, R13): never above the subtotal.</summary>
    public long DiscountFor(long eligibleSubtotalPaise)
    {
        var discount = DiscountType switch
        {
            DiscountType.Percent => Math.Min(BasisPoints.Apply(eligibleSubtotalPaise, (int)DiscountValue), MaxDiscountPaise ?? long.MaxValue),
            DiscountType.Flat => DiscountValue,
            _ => 0,
        };
        return Math.Clamp(discount, 0, Math.Max(eligibleSubtotalPaise, 0));
    }

    /// <summary>One more use (inside the checkout transaction). Refuses when the total limit is reached.</summary>
    public void RecordUse()
    {
        if (TotalLimit is { } total && UsedCount >= total)
        {
            throw new DomainException(PromotionsErrors.CouponLimitReached, "This coupon has reached its usage limit; remove it to continue.", "couponCode");
        }

        UsedCount++;
    }

    /// <summary>One use given back (redemption reversed).</summary>
    public void ReleaseUse() => UsedCount = Math.Max(0, UsedCount - 1);

    private bool IsEligible(CouponLineInput line)
        => _scopes.Count == 0
            || _scopes.Any(s => (s.ScopeType == CouponScopeType.Product && s.ScopeId == line.ProductId)
                || (s.ScopeType == CouponScopeType.Category && line.CategoryLineage.Contains(s.ScopeId)));

    private void Apply(CouponDefinition definition)
    {
        Description = string.IsNullOrWhiteSpace(definition.Description) ? null : definition.Description.Trim();
        DiscountType = definition.DiscountType;
        DiscountValue = definition.DiscountValue;
        MaxDiscountPaise = definition.MaxDiscountPaise;
        MinOrderPaise = definition.MinOrderPaise;
        CityId = definition.CityId;
        FirstOrderOnly = definition.FirstOrderOnly;
        PerCustomerLimit = definition.PerCustomerLimit;
        TotalLimit = definition.TotalLimit;
        ValidFrom = definition.ValidFrom;
        ValidTo = definition.ValidTo;
        IsListed = definition.IsListed;

        var wanted = definition.Scopes.Distinct().ToList();
        _scopes.RemoveAll(s => !wanted.Contains(new CouponScopeDefinition(s.ScopeType, s.ScopeId)));
        foreach (var scope in wanted.Where(w => !_scopes.Any(s => s.ScopeType == w.ScopeType && s.ScopeId == w.ScopeId)))
        {
            _scopes.Add(new CouponScope(scope.ScopeType, scope.ScopeId));
        }
    }

    private void EnsureNotExpired()
    {
        if (Status == CouponStatus.Expired)
        {
            throw new ConflictException(PromotionsErrors.InvalidStatusTransition, "An expired coupon cannot be changed.");
        }
    }

    private void ChangeStatus(CouponStatus to, Guid? actorId, DateTimeOffset now)
    {
        var from = Status;
        Status = to;
        Raise(new CouponStatusChanged(Id, Code, from.ToCode(), to.ToCode(), actorId, now));
    }

    private static void ThrowIfInvalid(IReadOnlyList<FieldError> errors)
    {
        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }
    }

    [GeneratedRegex("^[A-Z0-9]{3,20}$")]
    private static partial Regex CodePattern();
}
