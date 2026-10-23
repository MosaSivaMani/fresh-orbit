using AwesomeAssertions;
using FreshOrbit.SharedKernel.Pricing;

namespace FreshOrbit.SharedKernel.Tests.Unit;

public sealed class LinePricingTests
{
    private static LinePriceFacts Grams(long perKg, long charge = 0, PreparationChargeBasis? basis = null, int taxBp = 0)
        => new(PricedUnit.Gram, perKg, charge, basis, taxBp);

    [Fact]
    public void Gram_line_with_per_kg_preparation_charge()
    {
        var amounts = LinePricing.Compute(Grams(129_900, 4_000, PreparationChargeBasis.PerKg), 500);

        amounts.ItemPaise.Should().Be(64_950);
        amounts.PreparationPaise.Should().Be(2_000);
        amounts.TotalPaise.Should().Be(66_950);
    }

    [Fact]
    public void Gram_item_amount_rounds_half_up_per_line()
    {
        // 333 g at 89 950 / kg = 29 953.35 → 29 953
        LinePricing.Compute(Grams(89_950), 333).ItemPaise.Should().Be(29_953);

        // 1 g at 500 / kg = 0.5 → 1 (half up)
        LinePricing.Compute(Grams(500), 1).ItemPaise.Should().Be(1);

        // 3 g at 500 / kg = 1.5 → 2; 1 g at 499 / kg = 0.499 → 0
        LinePricing.Compute(Grams(500), 3).ItemPaise.Should().Be(2);
        LinePricing.Compute(Grams(499), 1).ItemPaise.Should().Be(0);
    }

    [Fact]
    public void Per_kg_preparation_rounds_half_up()
    {
        // 6 000 / kg × 333 g = 1 998; 4 500 / kg × 333 g = 1 498.5 → 1 499
        LinePricing.Compute(Grams(0, 6_000, PreparationChargeBasis.PerKg), 333).PreparationPaise.Should().Be(1_998);
        LinePricing.Compute(Grams(0, 4_500, PreparationChargeBasis.PerKg), 333).PreparationPaise.Should().Be(1_499);
    }

    [Fact]
    public void Piece_line_multiplies_unit_price_and_per_item_charge()
    {
        var amounts = LinePricing.Compute(new LinePriceFacts(PricedUnit.Piece, 65_000, 1_500, PreparationChargeBasis.PerItem, 0), 2);

        amounts.ItemPaise.Should().Be(130_000);
        amounts.PreparationPaise.Should().Be(3_000);
        amounts.TotalPaise.Should().Be(133_000);
    }

    [Fact]
    public void Flat_preparation_is_charged_once_per_line_and_not_on_an_empty_line()
    {
        LinePricing.Compute(Grams(100_000, 2_500, PreparationChargeBasis.Flat), 1_000).PreparationPaise.Should().Be(2_500);
        LinePricing.Compute(Grams(100_000, 2_500, PreparationChargeBasis.Flat), 2_000).PreparationPaise.Should().Be(2_500);
        LinePricing.Compute(Grams(100_000, 2_500, PreparationChargeBasis.Flat), 0).TotalPaise.Should().Be(0);
    }

    [Fact]
    public void No_charge_basis_means_no_preparation_charge()
        => LinePricing.Compute(Grams(100_000, 2_500), 500).PreparationPaise.Should().Be(0);

    [Theory]
    [InlineData(PricedUnit.Gram, PreparationChargeBasis.PerItem)]
    [InlineData(PricedUnit.Piece, PreparationChargeBasis.PerKg)]
    public void Unsupported_charge_basis_is_refused(PricedUnit unit, PreparationChargeBasis basis)
    {
        LinePricing.IsSupported(unit, basis).Should().BeFalse();
        var compute = () => LinePricing.Compute(new LinePriceFacts(unit, 100, 100, basis, 0), 1);
        compute.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(10_500, 500, 500)]
    [InlineData(66_950, 0, 0)]
    [InlineData(10_000, 500, 476)] // 476.19 → 476
    [InlineData(21, 500, 1)] // 1.0
    [InlineData(31, 1_800, 5)] // 4.72 → 5
    [InlineData(118, 1_800, 18)] // tax-inclusive 118 at 18 % contains exactly 18
    public void Contained_tax_is_extracted_from_the_tax_inclusive_amount(long amount, int rateBp, long expectedTax)
        => LinePricing.ContainedTax(amount, rateBp).Should().Be(expectedTax);

    [Fact]
    public void Contained_tax_exact_half_rounds_up()
        // 21 × 10 000 / 20 000 (rate 10 000 bp = 100 %) = 10.5 → 11
        => LinePricing.ContainedTax(21, 10_000).Should().Be(11);

    [Theory]
    [InlineData(500, true, 1_000, 550)]
    [InlineData(333, true, 1_000, 366)]
    [InlineData(500, false, 1_000, 500)]
    [InlineData(2, false, 1_000, 2)]
    [InlineData(999, true, 1_000, 1_098)] // 1 098.9 floored
    public void Max_chargeable_quantity_floors_the_overweight_allowance(int ordered, bool weightVariable, int overweightBp, int expected)
        => LinePricing.MaxChargeableQuantity(ordered, weightVariable, overweightBp).Should().Be(expected);

    [Theory]
    [InlineData(540, 550, 540)]
    [InlineData(580, 550, 550)]
    [InlineData(0, 550, 0)]
    public void Charged_quantity_is_capped_at_max_chargeable(int packed, int max, int expected)
        => LinePricing.ChargedQuantity(packed, max).Should().Be(expected);

    [Fact]
    public void Api_pack_example_final_lines()
    {
        var seer = Grams(129_900, 4_000, PreparationChargeBasis.PerKg);
        var prawns = Grams(89_900, 6_000, PreparationChargeBasis.PerKg);
        var crab = new LinePriceFacts(PricedUnit.Piece, 65_000, 0, PreparationChargeBasis.Flat, 0);

        LinePricing.Compute(seer, 540).TotalPaise.Should().Be(72_306);
        LinePricing.Compute(seer, 550).TotalPaise.Should().Be(73_645);
        LinePricing.Compute(prawns, LinePricing.ChargedQuantity(580, LinePricing.MaxChargeableQuantity(500, true, 1_000))).TotalPaise.Should().Be(52_745);
        LinePricing.Compute(prawns, 500).TotalPaise.Should().Be(47_950);
        LinePricing.Compute(crab, 2).TotalPaise.Should().Be(130_000);
    }
}
