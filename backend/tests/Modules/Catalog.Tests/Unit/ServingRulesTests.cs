using AwesomeAssertions;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Pricing;

namespace FreshOrbit.Catalog.Tests.Unit;

/// <summary>catalog.md CAT-R21 (serving facts) and CAT-R22 (compare-at discount).</summary>
public sealed class ServingRulesTests
{
    private static readonly PreparationSpec[] Whole = [new(Guid.CreateVersion7(), "WHOLE", 10000, true)];

    private static List<FieldError> Validate(SellUnit unit, int? piecesMin, int? piecesMax, int? serves)
    {
        var spec = new VariantSpec("Medium", unit, unit == SellUnit.Pack ? 500 : null, unit == SellUnit.Piece ? 450 : null, unit == SellUnit.Kg ? null : 1, unit == SellUnit.Kg ? null : 10, unit == SellUnit.Kg ? null : 1, Whole, piecesMin, piecesMax, serves);
        var errors = new List<FieldError>();
        ServingRules.Validate(spec, string.Empty, errors);
        return errors;
    }

    [Theory]
    [InlineData(SellUnit.Kg, 10, 12, 4)]
    [InlineData(SellUnit.Pack, 6, 8, 2)]
    [InlineData(SellUnit.Piece, null, null, 2)]
    [InlineData(SellUnit.Kg, null, null, null)]
    public void Valid_serving_facts_pass(SellUnit unit, int? min, int? max, int? serves)
        => Validate(unit, min, max, serves).Should().BeEmpty();

    [Theory]
    [InlineData(SellUnit.Kg, 12, 10, null, "piecesMax")]
    [InlineData(SellUnit.Kg, 10, null, null, "piecesMin")]
    [InlineData(SellUnit.Kg, 0, 2, null, "piecesMin")]
    [InlineData(SellUnit.Piece, 1, 1, null, "piecesMin")]
    [InlineData(SellUnit.Kg, null, null, 0, "serves")]
    [InlineData(SellUnit.Pack, null, null, 101, "serves")]
    public void Invalid_serving_facts_are_refused_by_field(SellUnit unit, int? min, int? max, int? serves, string field)
        => Validate(unit, min, max, serves).Select(e => e.Field).Should().Equal(field);

    [Fact]
    public void Kg_basis_is_one_kilogram_and_quantities_scale_down_with_floor_and_ceiling()
    {
        var basis = ServingRules.ForBasis(SellUnit.Kg, null, null, 10, 12, 4);
        basis.Should().Be(new ServingFacts(1000, QuantityUnit.Gram, 1000, 10, 12, 4));

        var half = ServingRules.ForQuantity(SellUnit.Kg, null, null, 10, 12, 4, 750);
        half.Should().Be(new ServingFacts(750, QuantityUnit.Gram, 750, 7, 9, 3));

        var small = ServingRules.ForQuantity(SellUnit.Kg, null, null, 2, 3, 1, 250);
        small.PiecesMin.Should().Be(1);
        small.Serves.Should().Be(1);
    }

    [Fact]
    public void Pack_and_piece_net_weight_comes_from_pack_size_and_average_piece()
    {
        ServingRules.ForQuantity(SellUnit.Pack, 500, null, 6, 8, 2, 3).Should().Be(new ServingFacts(3, QuantityUnit.Pack, 1500, 18, 24, 6));
        ServingRules.ForBasis(SellUnit.Piece, null, 450, null, null, 2).Should().Be(new ServingFacts(1, QuantityUnit.Piece, 450, null, null, 2));
        ServingRules.ForBasis(SellUnit.Piece, null, null, null, null, null).NetWeightGrams.Should().BeNull();
    }

    [Theory]
    [InlineData(129900L, 149900L, 1334)]
    [InlineData(50000L, 100000L, 5000)]
    public void Compare_at_discount_is_floored_basis_points(long price, long compareAt, int expected)
        => CompareAtDiscount.Bp(price, compareAt).Should().Be(expected);

    [Theory]
    [InlineData(129900L, null)]
    [InlineData(129900L, 129900L)]
    [InlineData(129900L, 99900L)]
    [InlineData(99999L, 100000L)]
    public void No_discount_without_a_higher_compare_at_price(long price, long? compareAt)
        => CompareAtDiscount.Bp(price, compareAt).Should().BeNull();
}
