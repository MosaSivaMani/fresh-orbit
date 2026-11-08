using AwesomeAssertions;
using FreshOrbit.Cart.Application.Pricing;
using FreshOrbit.Cart.Public;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Pricing.Public;

namespace FreshOrbit.Cart.Tests.Unit;

public sealed class LineCalculatorTests
{
    private static VariantInfo Fish(int minOrder = 250, int step = 250) => new(
        Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Seer Fish", "seer-fish", "Medium", "SEER-M", null, "0302", "SEAFOOD",
        SellUnit.Kg, StockUnit.Gram, null, null, minOrder, 5000, step, true, true, []);

    private static VariantInfo Crabs() => new(
        Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Mud Crab", "mud-crab", "Live", "CRAB-L", null, "0306", "SEAFOOD",
        SellUnit.Piece, StockUnit.Piece, null, 500, 1, 10, 1, false, true, []);

    [Theory]
    [InlineData(500, 8000, 625)]
    [InlineData(500, 10000, 500)]
    [InlineData(1000, 6500, 1539)]
    public void Gross_grams_round_up_after_yield(int net, int yieldBp, int gross)
        => LineCalculator.GrossQuantity(StockUnit.Gram, net, yieldBp).Should().Be(gross);

    [Fact]
    public void Pieces_ignore_yield()
        => LineCalculator.GrossQuantity(StockUnit.Piece, 2, 8000).Should().Be(2);

    [Fact]
    public void Available_net_rounds_down_to_the_order_step()
        => LineCalculator.AvailableNet(Fish(), 1000, 8000).Should().Be(750);

    [Fact]
    public void Availability_statuses()
    {
        var fish = Fish();
        LineCalculator.Availability(fish, null, 625, 8000, null).Status.Should().Be(LineAvailability.NotSoldHere);
        LineCalculator.Availability(fish, new VariantAvailability(fish.VariantId, 5000, QuantityUnit.Gram), 625, 8000, 1000).Status.Should().Be(LineAvailability.InStock);
        LineCalculator.Availability(fish, new VariantAvailability(fish.VariantId, 900, QuantityUnit.Gram), 625, 8000, 1000).Status.Should().Be(LineAvailability.LowStock);

        var partial = LineCalculator.Availability(fish, new VariantAvailability(fish.VariantId, 400, QuantityUnit.Gram), 625, 8000, null);
        partial.Status.Should().Be(LineAvailability.OutOfStock);
        partial.Message.Should().Contain("250 g");

        LineCalculator.Availability(fish, new VariantAvailability(fish.VariantId, 100, QuantityUnit.Gram), 625, 8000, null).Message.Should().Be("Out of stock today.");
        var crabs = Crabs();
        LineCalculator.Availability(crabs, new VariantAvailability(crabs.VariantId, -3, QuantityUnit.Piece), 2, 10000, null).Status.Should().Be(LineAvailability.OutOfStock);
    }

    [Fact]
    public void Quantities_format_in_the_sell_unit()
    {
        LineCalculator.FormatQuantity(Fish(), 750).Should().Be("750 g");
        LineCalculator.FormatQuantity(Fish(), 1500).Should().Be("1.5 kg");
        LineCalculator.FormatQuantity(Crabs(), 1).Should().Be("1 piece");
        LineCalculator.QuantityUnitCode(SellUnit.Kg).Should().Be("GRAM");
        LineCalculator.QuantityUnitCode(SellUnit.Pack).Should().Be("PACK");
    }

    [Fact]
    public void Line_view_carries_the_order_rules_the_cart_validates_with()
    {
        var fish = Fish(minOrder: 500, step: 250);
        var line = new PricingLine(Guid.CreateVersion7(), fish.VariantId, null, 750);

        // Unpriced (not sold at the location): the rules are still shown so the quantity editor works.
        var view = LineCalculator.Price(new ResolvedLine(line, fish, null, null), null, null, 1000).ToView(0);

        view.MinOrder.Should().Be(new QuantityView(500, "GRAM"));
        view.MaxOrder.Should().Be(new QuantityView(5000, "GRAM"));
        view.OrderStep.Should().Be(new QuantityView(250, "GRAM"));

        var crabs = Crabs();
        var crabView = LineCalculator.Price(new ResolvedLine(new PricingLine(Guid.CreateVersion7(), crabs.VariantId, null, 2), crabs, null, null), null, null, 0).ToView(0);
        crabView.MinOrder.Should().Be(new QuantityView(1, "PIECE"));
        crabView.MaxOrder!.Value.Should().Be(10);
        crabView.OrderStep!.Value.Should().Be(1);

        var gone = LineCalculator.Price(new ResolvedLine(line, null, null, "PRODUCT_NOT_AVAILABLE"), null, null, 0).ToView(0);
        gone.MinOrder.Should().BeNull();
        gone.MaxOrder.Should().BeNull();
        gone.OrderStep.Should().BeNull();
    }

    [Fact]
    public void With_no_overweight_allowance_the_max_line_total_is_the_shown_price()
    {
        // D347: the reference default allowance is 0 — the price shown is the most the customer pays.
        var fish = Fish();
        var line = new PricingLine(Guid.CreateVersion7(), fish.VariantId, null, 750);
        var price = new LinePrice(fish.VariantId, null, 129900, null, 0, null, 500);
        var stock = new VariantAvailability(fish.VariantId, 5000, QuantityUnit.Gram);

        var none = LineCalculator.Price(new ResolvedLine(line, fish, null, null), price, stock, 0).ToView(0);
        var allowance = LineCalculator.Price(new ResolvedLine(line, fish, null, null), price, stock, 1000).ToView(0);

        none.MaxChargeableQuantity.Should().Be(new QuantityView(750, "GRAM"));
        none.MaxLineTotalPaise.Should().Be(none.EstimatedLineTotalPaise);
        none.PackNetGrams.Should().BeNull();
        allowance.MaxChargeableQuantity.Should().Be(new QuantityView(825, "GRAM"));
        allowance.MaxLineTotalPaise.Should().BeGreaterThan(allowance.EstimatedLineTotalPaise);
    }

    [Fact]
    public void Fixed_net_weight_pack_has_one_price_whatever_the_allowance()
    {
        var pack = new VariantInfo(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Seer Fish", "seer-fish", "Steaks 500 g net", "SEER-P500", null, "0302", "SEAFOOD",
            SellUnit.Pack, StockUnit.Piece, 500, null, 1, 10, 1, false, true, [])
        {
            PiecesMin = 5,
            PiecesMax = 7,
        };
        var line = new PricingLine(Guid.CreateVersion7(), pack.VariantId, null, 2);
        var price = new LinePrice(pack.VariantId, null, 74900, null, 0, null, 500);

        var view = LineCalculator.Price(new ResolvedLine(line, pack, null, null), price, new VariantAvailability(pack.VariantId, 10, QuantityUnit.Piece), 1000).ToView(0);

        view.MaxChargeableQuantity.Should().Be(new QuantityView(2, "PACK"));
        view.EstimatedLineTotalPaise.Should().Be(149800);
        view.MaxLineTotalPaise.Should().Be(149800);
        view.PackNetGrams.Should().Be(500);
        view.PiecesMin.Should().Be(5);
        view.PiecesMax.Should().Be(7);
    }
}
