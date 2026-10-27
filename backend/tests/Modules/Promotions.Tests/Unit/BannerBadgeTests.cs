using AwesomeAssertions;
using FreshOrbit.Promotions.Domain.Banners;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Promotions.Tests.Unit;

/// <summary>The optional banner badge: configured text shown as given, trimmed, at most 20 characters.</summary>
public sealed class BannerBadgeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 6, 0, 0, TimeSpan.Zero);

    private static PromotionDefinition Definition(string? badge)
        => new("Prawn week", null, null, PromotionLinkType.Coupon, "welcome100", null, Now, Now.AddDays(7), 10, true, badge);

    [Fact]
    public void Badge_is_trimmed_and_kept()
        => Promotion.Create(Definition("  20% OFF ")).Badge.Should().Be("20% OFF");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_badge_means_no_badge(string? badge)
        => Promotion.Create(Definition(badge)).Badge.Should().BeNull();

    [Fact]
    public void Too_long_badge_is_refused_on_the_badge_field()
    {
        var act = () => Promotion.Create(Definition(new string('X', Promotion.MaxBadgeLength + 1)));

        act.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "badge");
    }

    [Fact]
    public void Update_can_clear_the_badge()
    {
        var promotion = Promotion.Create(Definition("NEW"));

        promotion.Update(Definition(null));

        promotion.Badge.Should().BeNull();
    }
}
