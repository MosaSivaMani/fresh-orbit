using AwesomeAssertions;
using FreshOrbit.Promotions.Application.Abstractions;
using FreshOrbit.Promotions.Application.Coupons;
using FreshOrbit.Promotions.Application.Handlers;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.Promotions.Domain.Redemptions;
using FreshOrbit.Promotions.Public;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.Testing;
using NSubstitute;
using static FreshOrbit.Promotions.Tests.Unit.CouponRuleTests;

namespace FreshOrbit.Promotions.Tests.Unit;

public sealed class CouponRedemptionServiceTests
{
    private readonly ICouponRepository _coupons = Substitute.For<ICouponRepository>();
    private readonly IRedemptionRepository _redemptions = Substitute.For<IRedemptionRepository>();
    private readonly IPromotionsCatalog _catalog = Substitute.For<IPromotionsCatalog>();
    private readonly TestClock _clock = new(Now);

    private CouponRedemptionService Service => new(_coupons, _redemptions, _catalog, _clock);

    private static IReadOnlyList<CouponLine> Lines =>
    [
        new(Guid.Parse("00000000-0000-0000-0000-000000000001"), SeerFish, FishCategory, 66950),
        new(Guid.Parse("00000000-0000-0000-0000-000000000002"), Crab, SeafoodCategory, 130000),
    ];

    [Fact]
    public async Task Unknown_code_evaluates_as_invalid()
    {
        var evaluation = await Service.EvaluateAsync(new EvaluateCouponRequest(" nope ", null, CityId, false, Now, Lines), TestContext.Current.CancellationToken);

        evaluation.Code.Should().Be("NOPE");
        evaluation.Status.Should().Be(CouponApplicationStatus.Invalid);
        evaluation.RefusalCode.Should().Be("COUPON_INVALID");
    }

    [Fact]
    public async Task Category_scope_uses_catalog_lineage()
    {
        var coupon = Coupon.Create(Definition(scopes: [new CouponScopeDefinition(CouponScopeType.Category, SeafoodCategory)]));
        _coupons.FindByCodeAsync("FRESH100", false, Arg.Any<CancellationToken>()).Returns(coupon);
        _catalog.GetCategoryLineagesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, IReadOnlyCollection<Guid>> { [FishCategory] = [FishCategory, SeafoodCategory], [SeafoodCategory] = [SeafoodCategory] });

        var evaluation = await Service.EvaluateAsync(new EvaluateCouponRequest("fresh100", null, CityId, false, Now, Lines), TestContext.Current.CancellationToken);

        evaluation.Status.Should().Be(CouponApplicationStatus.Applied);
        evaluation.Allocations.Should().HaveCount(2);
    }

    [Fact]
    public async Task Per_customer_limit_reads_the_customers_applied_count()
    {
        var coupon = Coupon.Create(Definition(perCustomer: 1));
        var customerId = Guid.NewGuid();
        _coupons.FindByCodeAsync("FRESH100", true, Arg.Any<CancellationToken>()).Returns(coupon);
        _coupons.CountAppliedAsync(coupon.Id, customerId, Arg.Any<CancellationToken>()).Returns(1);

        var redeem = () => Service.RedeemAsync(new RedeemCouponRequest("FRESH100", customerId, Guid.NewGuid(), CityId, false, Now, Lines), TestContext.Current.CancellationToken);

        (await redeem.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("COUPON_LIMIT_REACHED");
        _redemptions.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Redeem_adds_a_redemption_and_returns_the_allocation()
    {
        var coupon = Coupon.Create(Definition());
        var orderId = Guid.NewGuid();
        _coupons.FindByCodeAsync("FRESH100", true, Arg.Any<CancellationToken>()).Returns(coupon);

        var result = await Service.RedeemAsync(new RedeemCouponRequest("fresh100", Guid.NewGuid(), orderId, CityId, false, Now, Lines), TestContext.Current.CancellationToken);

        result.DiscountPaise.Should().Be(10000);
        result.Allocations.Sum(a => a.DiscountPaise).Should().Be(10000);
        coupon.UsedCount.Should().Be(1);
        _redemptions.Received(1).Add(Arg.Is<CouponRedemption>(r => r.OrderId == orderId && r.DiscountPaise == 10000));
    }

    [Fact]
    public async Task Redeem_refuses_a_coupon_that_is_not_applicable()
    {
        _coupons.FindByCodeAsync("FRESH100", true, Arg.Any<CancellationToken>()).Returns(Coupon.Create(Definition(minOrder: 500000)));

        var redeem = () => Service.RedeemAsync(new RedeemCouponRequest("FRESH100", Guid.NewGuid(), Guid.NewGuid(), CityId, false, Now, Lines), TestContext.Current.CancellationToken);

        (await redeem.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("COUPON_MIN_ORDER_NOT_MET");
    }

    [Fact]
    public async Task Reversal_is_a_no_op_without_an_applied_redemption()
    {
        var service = new RedemptionReversalService(_redemptions, _coupons, _clock);

        (await service.ReverseForOrderAsync(Guid.NewGuid(), ReversalReason.OrderCancelled, TestContext.Current.CancellationToken)).Should().BeFalse();

        var coupon = Coupon.Create(Definition());
        var redemption = CouponRedemption.Redeem(coupon, Guid.NewGuid(), Guid.NewGuid(), 10000, false, Now);
        _redemptions.FindByOrderAsync(redemption.OrderId, Arg.Any<CancellationToken>()).Returns(redemption);
        _coupons.FindByIdAsync(coupon.Id, Arg.Any<CancellationToken>()).Returns(coupon);

        (await service.ReverseForOrderAsync(redemption.OrderId, ReversalReason.PaymentFailed, TestContext.Current.CancellationToken)).Should().BeTrue();
        (await service.ReverseForOrderAsync(redemption.OrderId, ReversalReason.PaymentFailed, TestContext.Current.CancellationToken)).Should().BeFalse();
        coupon.UsedCount.Should().Be(0);
    }
}
