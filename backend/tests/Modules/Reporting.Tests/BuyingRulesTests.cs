using AwesomeAssertions;
using FreshOrbit.Reporting.Domain.Buying;

namespace FreshOrbit.Reporting.Tests;

/// <summary>Unit rules of the buying list (REP-R14) and today's prices (REP-R15).</summary>
public sealed class BuyingRulesTests
{
    [Theory]
    [InlineData("GRAM", 500, 6500, 770)]
    [InlineData("GRAM", 1000, 10000, 1000)]
    [InlineData("PIECE", 3, 6500, 3)]
    public void Needed_quantity_is_the_checkout_gross(string stockUnit, int ordered, int yieldBp, int expected)
        => BuyingRules.NeededQuantity(stockUnit, ordered, yieldBp).Should().Be(expected);

    [Fact]
    public void Net_grams_are_known_for_grams_and_packs_but_never_guessed_for_pieces()
    {
        BuyingRules.OrderedNetGrams("GRAM", "KG", 750, null).Should().Be(750);
        BuyingRules.OrderedNetGrams("PIECE", "PACK", 2, 500).Should().Be(1000);
        BuyingRules.OrderedNetGrams("PIECE", "PIECE", 2, null).Should().BeNull();
        BuyingRules.EstimatedRawGrams(1000, 7000).Should().Be(1429);
        BuyingRules.EstimatedRawGrams(null, 7000).Should().BeNull();
    }

    [Fact]
    public void Margin_rounds_up_and_shortfall_never_goes_negative()
    {
        BuyingRules.WithMargin(3309, 1000).Should().Be(3640);
        BuyingRules.WithMargin(2, 1000).Should().Be(3);
        BuyingRules.WithMargin(3309, 0).Should().Be(3309);
        BuyingRules.Available(5300, 5233, 3309).Should().Be(3376);
        BuyingRules.Available(null, 0, 0).Should().Be(0);
        BuyingRules.Available(100, 500, 0).Should().Be(0);
        BuyingRules.Shortfall(3640, 3376).Should().Be(264);
        BuyingRules.Shortfall(100, 3376).Should().Be(0);
        BuyingRules.ShortfallRawGrams("GRAM", 264, 3640, 3640).Should().Be(264);
        BuyingRules.ShortfallRawGrams("PIECE", 3, 3, 1572).Should().Be(1572);
        BuyingRules.ShortfallRawGrams("PIECE", 1, 3, 1572).Should().Be(524);
        BuyingRules.ShortfallRawGrams("PIECE", 1, 4, null).Should().BeNull();
    }

    [Fact]
    public void Cost_per_sell_unit_takes_the_cleaning_loss_for_kg_only()
    {
        PriceRules.CostPerSellUnitPaise("KG", 455_000, 10_000, 6500).Should().Be(70_000);
        PriceRules.CostPerSellUnitPaise("KG", 70_000, 1_000, 10000).Should().Be(70_000);
        PriceRules.CostPerSellUnitPaise("KG", 455_000, 10_000, null).Should().BeNull("a missing yield is never guessed");
        PriceRules.CostPerSellUnitPaise("PACK", 1_000_000, 20, null).Should().Be(50_000);
        PriceRules.CostPerSellUnitPaise("PIECE", 100, 3, 6500).Should().Be(33);
        PriceRules.CostPerSellUnitPaise("PIECE", 100, 0, null).Should().BeNull();
    }

    [Fact]
    public void Margin_is_on_the_price_without_gst_and_truncates()
    {
        PriceRules.PriceExTaxPaise(90_000, 500).Should().Be(85_714);
        PriceRules.PriceExTaxPaise(90_000, 0).Should().Be(90_000);
        PriceRules.MarginBp(85_714, 70_000).Should().Be(1833);
        PriceRules.MarginBp(50_000, 60_000).Should().Be(-2000);
        PriceRules.MarginBp(0, 10).Should().BeNull();
    }

    [Theory]
    [InlineData(70_000, 2500, 500, 98_100)]
    [InlineData(50_000, 3000, 500, 75_100)]
    [InlineData(70_000, 0, 0, 70_000)]
    [InlineData(69_901, 0, 0, 70_000)]
    public void Suggested_price_reaches_the_target_and_rounds_up_to_rupees(long cost, int targetBp, int taxBp, long expected)
    {
        var suggested = PriceRules.SuggestedPricePaise(cost, targetBp, taxBp);

        suggested.Should().Be(expected);
        PriceRules.MarginBp(PriceRules.PriceExTaxPaise(suggested, taxBp), cost).Should().BeGreaterThanOrEqualTo(targetBp - 1);
    }

    [Fact]
    public void Suggested_price_refuses_a_target_above_95_percent()
    {
        var act = () => PriceRules.SuggestedPricePaise(70_000, 9600, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
