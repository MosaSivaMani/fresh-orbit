using AwesomeAssertions;
using FreshOrbit.Catalog.Application.Storefront;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Catalog.Tests.Unit;

public sealed class VariantRulesTests
{
    private static readonly Guid Whole = Guid.CreateVersion7();
    private static readonly Guid CurryCut = Guid.CreateVersion7();

    private static VariantSpec Kg(params PreparationSpec[] preparations)
        => new("Medium", SellUnit.Kg, null, null, null, null, null, preparations.Length == 0 ? [new(CurryCut, "CURRY_CUT", 6500, true)] : preparations);

    private static List<FieldError> Validate(VariantSpec spec, out VariantUnits units)
    {
        var errors = new List<FieldError>();
        units = VariantRules.Validate(spec, string.Empty, errors);
        return errors;
    }

    [Fact]
    public void Kg_variant_gets_gram_stock_weight_variable_and_default_quantity_rules()
    {
        var errors = Validate(Kg(), out var units);

        errors.Should().BeEmpty();
        units.Should().Be(new VariantUnits(SellUnit.Kg, StockUnit.Gram, true, 250, 5000, 250));
    }

    [Fact]
    public void Pack_needs_pack_size_and_piece_needs_average_weight()
    {
        Validate(Kg() with { SellUnit = SellUnit.Pack, MinOrder = 1, MaxOrder = 10, OrderStep = 1, Preparations = [new(Whole, "WHOLE", 10000, true)] }, out _)
            .Should().Contain(e => e.Field == "packSizeGrams" && e.Code == CatalogErrors.ValidationFailed);
        Validate(Kg() with { SellUnit = SellUnit.Piece, MinOrder = 1, MaxOrder = 10, OrderStep = 1, Preparations = [new(Whole, "WHOLE", 10000, true)] }, out var units)
            .Should().ContainSingle(e => e.Field == "avgPieceGrams");
        units.StockUnit.Should().Be(StockUnit.Piece);
        units.IsWeightVariable.Should().BeFalse();
    }

    [Fact]
    public void Kg_variant_with_pack_size_is_refused()
        => Validate(Kg() with { PackSizeGrams = 500 }, out _).Should().ContainSingle(e => e.Field == "packSizeGrams");

    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public void Yield_outside_1_to_10000_is_yield_invalid(int yieldBp)
        => Validate(Kg(new PreparationSpec(CurryCut, "CURRY_CUT", yieldBp, true)), out _)
            .Should().ContainSingle(e => e.Field == "preparations[0].yieldBp" && e.Code == CatalogErrors.YieldInvalid);

    [Fact]
    public void Piece_variant_must_use_full_yield_and_whole_is_always_10000()
    {
        var piece = new VariantSpec("Crab", SellUnit.Piece, null, 400, 1, 10, 1, [new(CurryCut, "CURRY_CUT", 7000, true)]);
        Validate(piece, out _).Should().ContainSingle(e => e.Field == "preparations[0].yieldBp" && e.Code == CatalogErrors.ValidationFailed);

        Validate(Kg(new PreparationSpec(Whole, "WHOLE", 9000, true)), out _).Should().ContainSingle(e => e.Field == "preparations[0].yieldBp");
    }

    [Fact]
    public void Exactly_one_default_preparation_is_required()
    {
        Validate(Kg(new(Whole, "WHOLE", 10000, true), new(CurryCut, "CURRY_CUT", 6500, true)), out _)
            .Should().ContainSingle(e => e.Code == CatalogErrors.DefaultPreparationRequired);
        Validate(Kg(new PreparationSpec(Whole, "WHOLE", 10000, false)), out _)
            .Should().ContainSingle(e => e.Code == CatalogErrors.DefaultPreparationRequired);
        Validate(Kg() with { Preparations = [] }, out _).Should().ContainSingle(e => e.Field == "preparations");
    }

    [Fact]
    public void Quantity_configuration_follows_cat_r7()
    {
        Validate(Kg() with { MinOrder = 300, OrderStep = 250 }, out _).Should().ContainSingle(e => e.Field == "minOrder");
        Validate(Kg() with { MaxOrder = 200 }, out _).Should().Contain(e => e.Field == "maxOrder");
        Validate(Kg() with { OrderStep = 0 }, out _).Should().ContainSingle(e => e.Field == "orderStep");
    }

    [Theory]
    [InlineData(600, QuantityUnit.Gram, CatalogErrors.QuantityStepInvalid)]
    [InlineData(4, QuantityUnit.Piece, CatalogErrors.QuantityUnitInvalid)]
    [InlineData(100, QuantityUnit.Gram, CatalogErrors.QuantityBelowMin)]
    [InlineData(5250, QuantityUnit.Gram, CatalogErrors.QuantityAboveMax)]
    [InlineData(750, QuantityUnit.Gram, null)]
    public void Quantities_are_checked_against_the_variant_rules(int quantity, QuantityUnit unit, string? expected)
        => QuantityRules.Check(quantity, unit, SellUnit.Kg, 250, 5000, 250).Should().Be(expected);

    [Theory]
    [InlineData(1000, 6500, 500)]
    [InlineData(1000, 10000, 1000)]
    [InlineData(300, 6500, 0)]
    [InlineData(100_000, 6500, 5000)]
    public void Max_orderable_converts_gross_stock_to_net_with_the_yield(int available, int yieldBp, int expected)
        => StockMath.MaxOrderable(available, SellUnit.Kg, yieldBp, 250, 5000, 250).Should().Be(expected);

    [Fact]
    public void Stock_status_uses_the_low_stock_threshold()
    {
        StockMath.Status(0, 500).Should().Be(AvailabilityStatus.OutOfStock);
        StockMath.Status(500, 500).Should().Be(AvailabilityStatus.LowStock);
        StockMath.Status(501, 500).Should().Be(AvailabilityStatus.InStock);
    }
}
