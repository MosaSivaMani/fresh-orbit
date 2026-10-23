using AwesomeAssertions;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.SharedKernel.Tests.Unit;

public sealed class UnitsTests
{
    [Theory]
    [InlineData(5, 2, 3)]
    [InlineData(-5, 2, -3)]
    [InlineData(4, 3, 1)]
    [InlineData(5, 3, 2)]
    [InlineData(-5, 3, -2)]
    [InlineData(7, -2, -4)]
    [InlineData(6, 2, 3)]
    public void DivideHalfAwayFromZero_rounds_halves_away_from_zero(long numerator, long denominator, long expected)
        => Rounding.DivideHalfAwayFromZero(numerator, denominator).Should().Be(expected);

    [Theory]
    [InlineData(10_001, 500, 500)]
    [InlineData(10_010, 500, 501)]
    [InlineData(10_000, 1_800, 1_800)]
    [InlineData(-10_010, 500, -501)]
    [InlineData(1, 5_000, 1)]
    public void BasisPoints_apply_rounds_half_away_from_zero(long amount, int bp, long expected)
        => BasisPoints.Apply(amount, bp).Should().Be(expected);

    [Fact]
    public void BasisPoints_increase_and_ratio()
    {
        BasisPoints.Increase(500, 1_000).Should().Be(550);
        BasisPoints.Ratio(1, 3).Should().Be(3_333);
        BasisPoints.Ratio(2, 3).Should().Be(6_667);
    }

    [Theory]
    [InlineData(60_000, 750, 45_000)]
    [InlineData(59_999, 500, 30_000)]
    [InlineData(99_999, 333, 33_300)]
    public void Money_for_grams_prices_by_per_kg_rate(long pricePerKg, int grams, long expected)
        => Money.ForGrams(pricePerKg, grams).Should().Be(expected);

    [Fact]
    public void Money_overflow_throws()
    {
        var multiply = () => Money.Multiply(long.MaxValue, 2);
        multiply.Should().Throw<OverflowException>();
        var apply = () => BasisPoints.Apply(long.MaxValue, 20_000);
        apply.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Money_allocate_splits_exactly()
    {
        var parts = Money.Allocate(100, [1, 1, 1]);
        parts.Should().Equal(34, 33, 33);
        Money.Allocate(1_000, [300, 700]).Should().Equal(300, 700);
        Money.Allocate(0, [5, 5]).Should().Equal(0, 0);
    }

    [Fact]
    public void Money_formats_rupees() => Money.ToRupeeString(12_345).Should().Be("123.45");

    [Theory]
    [InlineData(500, 6_500, 770)]
    [InlineData(1_000, 10_000, 1_000)]
    [InlineData(1, 3_000, 4)]
    public void Weight_gross_for_net_rounds_up(int net, int yieldBp, int expected)
        => Weight.GrossForNet(net, yieldBp).Should().Be(expected);

    [Fact]
    public void Weight_rejects_invalid_yield()
    {
        var gross = () => Weight.GrossForNet(500, 0);
        gross.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Weight_max_chargeable_adds_overweight_allowance()
        => Weight.MaxChargeable(1_000, 1_000).Should().Be(1_100);
}
