using AwesomeAssertions;
using FreshOrbit.Promotions.Application.Coupons;
using FreshOrbit.Promotions.Domain.Coupons;

namespace FreshOrbit.Promotions.Tests.Unit;

/// <summary>promotions.md PROMO-R26: the coupon line shown on a product.</summary>
public sealed class ProductOfferTests
{
    private static readonly Guid Product = Guid.CreateVersion7();
    private static readonly Guid Fish = Guid.CreateVersion7();
    private static readonly Guid Seafood = Guid.CreateVersion7();
    private static readonly Guid[] Lineage = [Fish, Seafood];

    [Fact]
    public void Product_scope_beats_category_scope_beats_store_wide()
    {
        var storeWide = Coupon.Create(CouponRuleTests.Definition(code: "ALL50"));
        var category = Coupon.Create(CouponRuleTests.Definition(code: "SEA75", scopes: [new CouponScopeDefinition(CouponScopeType.Category, Seafood)]));
        var product = Coupon.Create(CouponRuleTests.Definition(code: "SEER10", scopes: [new CouponScopeDefinition(CouponScopeType.Product, Product)]));

        ProductOfferService.Pick([storeWide, category, product], Product, Lineage)!.Code.Should().Be("SEER10");
        ProductOfferService.Pick([storeWide, category], Product, Lineage)!.Code.Should().Be("SEA75");
        ProductOfferService.Pick([storeWide], Product, Lineage)!.Code.Should().Be("ALL50");
    }

    [Fact]
    public void Coupons_scoped_elsewhere_do_not_apply_and_ties_take_the_lower_minimum()
    {
        var elsewhere = Coupon.Create(CouponRuleTests.Definition(code: "PRAWN", scopes: [new CouponScopeDefinition(CouponScopeType.Category, Guid.CreateVersion7())]));
        ProductOfferService.Pick([elsewhere], Product, Lineage).Should().BeNull();

        var high = Coupon.Create(CouponRuleTests.Definition(code: "AAA999", minOrder: 99900));
        var low = Coupon.Create(CouponRuleTests.Definition(code: "ZZZ499", minOrder: 49900));
        ProductOfferService.Pick([high, low], Product, Lineage)!.Code.Should().Be("ZZZ499");
    }

    [Theory]
    [InlineData(DiscountType.Percent, 1500L, 10000L, 0L, false, "15% off up to ₹100 with code CODE1")]
    [InlineData(DiscountType.Percent, 1250L, 9950L, 49900L, false, "12.5% off up to ₹99.50 on orders above ₹499 with code CODE1")]
    [InlineData(DiscountType.Flat, 5000L, null, 0L, true, "₹50 off on your first order with code CODE1")]
    [InlineData(DiscountType.FreeDelivery, 0L, null, 0L, false, "Free delivery with code CODE1")]
    public void Text_is_composed_by_the_server(DiscountType type, long value, long? max, long minOrder, bool firstOrder, string expected)
        => ProductOfferService.Text(Coupon.Create(CouponRuleTests.Definition(type, value, max, minOrder, firstOrderOnly: firstOrder, perCustomer: firstOrder ? 1 : null, code: "CODE1"))).Should().Be(expected);
}
