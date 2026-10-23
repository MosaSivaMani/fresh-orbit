using AwesomeAssertions;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.Promotions.Domain.Redemptions;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Promotions.Tests.Unit;

public sealed class CouponRuleTests
{
    public static readonly DateTimeOffset Now = new(2026, 10, 23, 6, 0, 0, TimeSpan.Zero);
    public static readonly Guid CityId = Guid.CreateVersion7();
    public static readonly Guid SeafoodCategory = Guid.CreateVersion7();
    public static readonly Guid FishCategory = Guid.CreateVersion7();
    public static readonly Guid SeerFish = Guid.CreateVersion7();
    public static readonly Guid Crab = Guid.CreateVersion7();

    public static CouponDefinition Definition(
        DiscountType type = DiscountType.Flat,
        long value = 10000,
        long? max = null,
        long minOrder = 0,
        Guid? cityId = null,
        bool firstOrderOnly = false,
        int? perCustomer = null,
        int? total = null,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validTo = null,
        string code = "fresh100",
        IReadOnlyList<CouponScopeDefinition>? scopes = null)
        => new(code, "₹100 off", type, value, max, minOrder, cityId, firstOrderOnly, perCustomer, total, validFrom ?? Now.AddDays(-1), validTo, true, scopes ?? []);

    /// <summary>Seer fish ₹669.50 (category FISH under SEAFOOD) and two crabs ₹1,300.</summary>
    public static CouponEvaluationContext Cart(bool hasQualifyingOrder = false, int applied = 0, Guid? cityId = null, DateTimeOffset? at = null) => new(
        at ?? Now,
        cityId ?? CityId,
        hasQualifyingOrder,
        applied,
        [
            new CouponLineInput(Guid.Parse("00000000-0000-0000-0000-000000000001"), SeerFish, [FishCategory, SeafoodCategory], 66950),
            new CouponLineInput(Guid.Parse("00000000-0000-0000-0000-000000000002"), Crab, [SeafoodCategory], 130000),
        ]);

    [Fact]
    public void Code_is_trimmed_and_upper_cased()
        => Coupon.Create(Definition(code: "  fresh100 ")).Code.Should().Be("FRESH100");

    [Fact]
    public void Flat_coupon_applies_and_allocates_over_eligible_lines_by_amount()
    {
        var outcome = Coupon.Create(Definition()).Evaluate(Cart());

        outcome.Status.Should().Be(CouponOutcomeStatus.Applied);
        outcome.DiscountPaise.Should().Be(10000);
        outcome.EligibleSubtotalPaise.Should().Be(196950);
        outcome.Allocations.Select(a => a.DiscountPaise).Should().Equal(3399, 6601);
        outcome.Allocations.Sum(a => a.DiscountPaise).Should().Be(10000);
    }

    [Fact]
    public void Percent_coupon_is_capped_at_max_discount()
    {
        Coupon.Create(Definition(DiscountType.Percent, 1000, max: 15000)).Evaluate(Cart()).DiscountPaise.Should().Be(15000);
        Coupon.Create(Definition(DiscountType.Percent, 500, max: 15000)).Evaluate(Cart()).DiscountPaise.Should().Be(9848);
    }

    [Fact]
    public void Flat_discount_never_exceeds_the_eligible_subtotal()
        => Coupon.Create(Definition(value: 500000)).Evaluate(Cart()).DiscountPaise.Should().Be(196950);

    [Fact]
    public void Free_delivery_waives_the_fee_with_zero_discount()
    {
        var outcome = Coupon.Create(Definition(DiscountType.FreeDelivery, 0)).Evaluate(Cart());

        outcome.Status.Should().Be(CouponOutcomeStatus.Applied);
        outcome.DiscountPaise.Should().Be(0);
        outcome.WaivesDeliveryFee.Should().BeTrue();
    }

    [Fact]
    public void Category_scope_includes_descendants_and_product_scope_matches_the_product()
    {
        var byCategory = Coupon.Create(Definition(scopes: [new CouponScopeDefinition(CouponScopeType.Category, FishCategory)])).Evaluate(Cart());
        byCategory.Allocations.Should().ContainSingle().Which.DiscountPaise.Should().Be(10000);
        byCategory.EligibleSubtotalPaise.Should().Be(66950);

        var byParent = Coupon.Create(Definition(scopes: [new CouponScopeDefinition(CouponScopeType.Category, SeafoodCategory)])).Evaluate(Cart());
        byParent.EligibleSubtotalPaise.Should().Be(196950);

        var byProduct = Coupon.Create(Definition(scopes: [new CouponScopeDefinition(CouponScopeType.Product, Crab)])).Evaluate(Cart());
        byProduct.EligibleSubtotalPaise.Should().Be(130000);

        var nothing = Coupon.Create(Definition(scopes: [new CouponScopeDefinition(CouponScopeType.Product, Guid.NewGuid())])).Evaluate(Cart());
        nothing.Status.Should().Be(CouponOutcomeStatus.Invalid);
        nothing.Code.Should().Be("COUPON_INVALID");
    }

    [Fact]
    public void Min_order_is_checked_on_the_eligible_subtotal_and_is_fixable()
    {
        var outcome = Coupon.Create(Definition(minOrder: 100000, scopes: [new CouponScopeDefinition(CouponScopeType.Category, FishCategory)])).Evaluate(Cart());

        outcome.Status.Should().Be(CouponOutcomeStatus.NotApplicable);
        outcome.Code.Should().Be("COUPON_MIN_ORDER_NOT_MET");
        Coupon.Create(Definition(minOrder: 196950)).Evaluate(Cart()).Status.Should().Be(CouponOutcomeStatus.Applied);
    }

    [Fact]
    public void Validity_window_is_from_inclusive_to_exclusive()
    {
        Coupon.Create(Definition(validFrom: Now.AddMinutes(1))).Evaluate(Cart()).Code.Should().Be("COUPON_NOT_STARTED");
        Coupon.Create(Definition(validFrom: Now)).Evaluate(Cart()).Status.Should().Be(CouponOutcomeStatus.Applied);
        Coupon.Create(Definition(validTo: Now)).Evaluate(Cart()).Code.Should().Be("COUPON_EXPIRED");
        Coupon.Create(Definition(validTo: Now.AddSeconds(1))).Evaluate(Cart()).Status.Should().Be(CouponOutcomeStatus.Applied);
    }

    [Fact]
    public void City_coupon_is_invalid_elsewhere()
    {
        var coupon = Coupon.Create(Definition(cityId: CityId));

        coupon.Evaluate(Cart()).Status.Should().Be(CouponOutcomeStatus.Applied);
        coupon.Evaluate(Cart(cityId: Guid.NewGuid())).Code.Should().Be("COUPON_INVALID");
    }

    [Fact]
    public void First_order_only_refuses_a_customer_with_a_qualifying_order()
    {
        var coupon = Coupon.Create(Definition(firstOrderOnly: true, perCustomer: 1));

        coupon.Evaluate(Cart()).Status.Should().Be(CouponOutcomeStatus.Applied);
        coupon.Evaluate(Cart(hasQualifyingOrder: true)).Code.Should().Be("COUPON_FIRST_ORDER_ONLY");
    }

    [Fact]
    public void Per_customer_and_total_limits_count_applied_uses()
    {
        Coupon.Create(Definition(perCustomer: 2)).Evaluate(Cart(applied: 1)).Status.Should().Be(CouponOutcomeStatus.Applied);
        Coupon.Create(Definition(perCustomer: 2)).Evaluate(Cart(applied: 2)).Code.Should().Be("COUPON_LIMIT_REACHED");

        var limited = Coupon.Create(Definition(total: 1));
        limited.RecordUse();
        limited.Evaluate(Cart()).Code.Should().Be("COUPON_LIMIT_REACHED");
        FluentActions.Invoking(limited.RecordUse).Should().Throw<DomainException>().Which.Code.Should().Be("COUPON_LIMIT_REACHED");
    }

    [Fact]
    public void Paused_and_expired_coupons_do_not_apply()
    {
        var coupon = Coupon.Create(Definition(validTo: Now.AddDays(1)));
        coupon.Pause(null, Now);
        coupon.Evaluate(Cart()).Code.Should().Be("COUPON_INVALID");

        coupon.ExpireIfDue(Now).Should().BeFalse();
        coupon.ExpireIfDue(Now.AddDays(1)).Should().BeTrue();
        coupon.Status.Should().Be(CouponStatus.Expired);
        coupon.Evaluate(Cart()).Code.Should().Be("COUPON_EXPIRED");
        FluentActions.Invoking(() => coupon.Resume(null, Now)).Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
    }

    [Fact]
    public void Resume_past_valid_to_is_refused_and_pause_twice_is_a_conflict()
    {
        var coupon = Coupon.Create(Definition(validTo: Now.AddHours(1)));
        coupon.Pause(null, Now);

        FluentActions.Invoking(() => coupon.Pause(null, Now)).Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
        FluentActions.Invoking(() => coupon.Resume(null, Now.AddHours(2))).Should().Throw<ConflictException>();
        coupon.Resume(null, Now);
        coupon.Status.Should().Be(CouponStatus.Active);
    }

    [Theory]
    [InlineData("AB", DiscountType.Flat, 100L, null, "code")]
    [InlineData("FRESH-100", DiscountType.Flat, 100L, null, "code")]
    [InlineData("FRESH10", DiscountType.Percent, 0L, 5000L, "discountValue")]
    [InlineData("FRESH10", DiscountType.Percent, 10001L, 5000L, "discountValue")]
    [InlineData("FRESH10", DiscountType.Percent, 1000L, null, "maxDiscountPaise")]
    [InlineData("FRESH10", DiscountType.Flat, 0L, null, "discountValue")]
    [InlineData("FREEDEL", DiscountType.FreeDelivery, 100L, null, "discountValue")]
    public void Invalid_definitions_are_refused_by_field(string code, DiscountType type, long value, long? max, string field)
        => FluentActions.Invoking(() => Coupon.Create(Definition(type, value, max, code: code)))
            .Should().Throw<RefusalException>().Which.Errors.Should().Contain(e => e.Field == field && e.Code == "VALIDATION_FAILED");

    [Fact]
    public void First_order_coupon_needs_per_customer_limit_one_and_valid_to_after_valid_from()
    {
        Coupon.Validate(Definition(firstOrderOnly: true), 0).Should().Contain(e => e.Field == "perCustomerLimit");
        Coupon.Validate(Definition(validTo: Now.AddDays(-1)), 0).Should().Contain(e => e.Field == "validTo");
        Coupon.Validate(Definition(total: 2), usedCount: 3).Should().Contain(e => e.Field == "totalLimit");
        Coupon.Validate(Definition(), 0).Should().BeEmpty();
    }

    [Fact]
    public void Code_cannot_change_once_redeemed()
    {
        var coupon = Coupon.Create(Definition());

        FluentActions.Invoking(() => coupon.Update(Definition(code: "OTHER100"), hasRedemptions: true))
            .Should().Throw<RefusalException>().Which.Errors.Should().Contain(e => e.Field == "code");
        coupon.Update(Definition(code: "OTHER100"), hasRedemptions: false);
        coupon.Code.Should().Be("OTHER100");
    }

    [Fact]
    public void Redeem_takes_a_use_and_reverse_gives_it_back_once()
    {
        var coupon = Coupon.Create(Definition(total: 5));
        var redemption = CouponRedemption.Redeem(coupon, Guid.NewGuid(), Guid.NewGuid(), 10000, false, Now);
        coupon.UsedCount.Should().Be(1);
        redemption.Status.Should().Be(RedemptionStatus.Applied);

        redemption.Reverse(coupon, ReversalReason.OrderCancelled, Now).Should().BeTrue();
        redemption.Reverse(coupon, ReversalReason.PaymentFailed, Now).Should().BeFalse();

        coupon.UsedCount.Should().Be(0);
        redemption.ReversalReason.Should().Be(ReversalReason.OrderCancelled);
    }
}
