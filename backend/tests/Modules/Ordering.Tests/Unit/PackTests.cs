using AwesomeAssertions;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Ordering.Tests.Unit;

public sealed class PackTests
{
    private static readonly DateTimeOffset At = OrderFactory.PlacedAt.AddHours(2);

    [Fact]
    public void Api_example_pack_gives_the_documented_final_amounts()
    {
        var order = OrderFactory.ApiExample().Preparing();
        var (fish, prawns, crabs) = (order.Items[0], order.Items[1], order.Items[2]);

        var lines = order.Pack(
            [new PackLine(fish.Id, 540, "GRAM"), new PackLine(prawns.Id, 580, "GRAM"), new PackLine(crabs.Id, 2, "PIECE")],
            true,
            0,
            OrderFactory.Staff,
            At);

        lines.Select(l => l.FinalTotalPaise).Should().Equal(72306, 52745, 130000);
        lines.Select(l => l.AdjustmentPaise).Should().Equal(5356, 4795, 0);
        lines.Select(l => l.ChargedQuantity).Should().Equal(540, 550, 2);
        lines.Select(l => l.Capped).Should().Equal(false, true, false);
        order.FinalItemsPaise.Should().Be(249591);
        order.FinalPreparationPaise.Should().Be(5460);
        order.FinalDiscountPaise.Should().Be(10000);
        order.FinalTotalPaise.Should().Be(245051);
        order.AmountDuePaise.Should().Be(245051);
        order.RefundDuePaise.Should().Be(0);
        order.Status.Should().Be(OrderStatus.Packed);
        order.Adjustments.Where(a => a.Kind == AdjustmentKind.Weight).Select(a => a.AmountPaise).Should().BeEquivalentTo([5356L, 4795L]);

        var packed = order.DomainEvents.OfType<OrderPacked>().Should().ContainSingle().Subject;
        packed.FinalTotalPaise.Should().Be(245051);
        packed.ColdBoxUsed.Should().BeTrue();
        packed.Lines.Should().HaveCount(3);
        packed.Lines[1].PackedQuantity.Should().Be(580);
        packed.Lines[1].ChargedQuantity.Should().Be(550);
        packed.Lines[1].StockUnit.Should().Be("GRAM");
    }

    [Fact]
    public void Weight_variable_line_above_the_cap_is_charged_at_the_cap()
    {
        var order = OrderFactory.Place([OrderFactory.SeerFish()]).Preparing();
        var line = order.Items[0];

        var result = order.Pack([new PackLine(line.Id, 600, "GRAM")], false, 0, OrderFactory.Staff, At);

        line.MaxChargeableQuantity.Should().Be(550);
        result[0].ChargedQuantity.Should().Be(550);
        result[0].Capped.Should().BeTrue();
        result[0].PackedQuantity.Should().Be(600);
    }

    [Fact]
    public void With_no_overweight_allowance_a_heavier_pack_is_charged_the_ordered_weight()
    {
        // D347: the reference default allowance is 0 — the price shown is the most the customer pays.
        var order = OrderFactory.Place([NoAllowanceSeerFish()]).Preparing();
        var line = order.Items[0];

        var result = order.Pack([new PackLine(line.Id, 600, "GRAM")], false, 0, OrderFactory.Staff, At);

        line.MaxChargeableQuantity.Should().Be(500);
        order.MaxTotalPaise.Should().Be(order.TotalPaise);
        result[0].PackedQuantity.Should().Be(600);
        result[0].ChargedQuantity.Should().Be(500);
        result[0].Capped.Should().BeTrue();
        result[0].AdjustmentPaise.Should().Be(0);
        result[0].FinalTotalPaise.Should().Be(66950);
        order.Adjustments.Should().BeEmpty();
        order.FinalTotalPaise.Should().Be(order.TotalPaise);
        order.AmountDuePaise.Should().Be(order.TotalPaise);
    }

    [Fact]
    public void With_no_overweight_allowance_a_lighter_pack_reduces_the_charge()
    {
        var order = OrderFactory.Place([NoAllowanceSeerFish()]).Preparing();

        var result = order.Pack([new PackLine(order.Items[0].Id, 450, "GRAM")], false, 0, OrderFactory.Staff, At);

        // 450 g = 58455 + 1800 against 66950 ordered.
        result[0].ChargedQuantity.Should().Be(450);
        result[0].Capped.Should().BeFalse();
        result[0].FinalTotalPaise.Should().Be(60255);
        result[0].AdjustmentPaise.Should().Be(-6695);
        order.Adjustments.Should().ContainSingle(a => a.Kind == AdjustmentKind.Weight && a.AmountPaise == -6695);
        order.FinalTotalPaise.Should().Be(60255);
        order.AmountDuePaise.Should().Be(60255);
    }

    [Fact]
    public void With_no_overweight_allowance_a_prepaid_lighter_pack_refunds_the_shortfall()
    {
        var order = OrderFactory.Place([NoAllowanceSeerFish()], PaymentMethod.Online);
        var paid = order.TotalPaise;
        order.ConfirmPayment(Guid.NewGuid(), paid, order.PlacedAt.AddMinutes(1));
        order.Preparing();

        order.Pack([new PackLine(order.Items[0].Id, 450, "GRAM")], false, paid, OrderFactory.Staff, At);

        order.FinalTotalPaise.Should().Be(60255);
        order.AmountDuePaise.Should().Be(0);
        order.RefundDuePaise.Should().Be(paid - 60255);
        order.RefundDueWeightShortfallPaise.Should().Be(paid - 60255);
    }

    [Fact]
    public void Fixed_net_weight_pack_is_charged_the_pack_price_whatever_is_packed_above_it()
    {
        // A fixed pack (sell unit PACK, e.g. "Seer steaks, 500 g net") is never weight-variable: the charge is per pack.
        var pack = OrderFactory.Line("PACK", 74900, 0, null, 1, false, 0, 0, "Seer Fish", "Steaks 500 g net");
        var order = OrderFactory.Place([pack]).Preparing();
        var line = order.Items[0];

        var result = order.Pack([new PackLine(line.Id, 2, "PACK")], false, 0, OrderFactory.Staff, At);

        line.MaxChargeableQuantity.Should().Be(1);
        result[0].ChargedQuantity.Should().Be(1);
        result[0].FinalTotalPaise.Should().Be(74900);
        order.FinalTotalPaise.Should().Be(order.TotalPaise);
        order.Adjustments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(560)] // packed above the stated 500 g net: the extra is free
    [InlineData(500)]
    [InlineData(480)] // under-packed by mistake: still recorded, the charge is the pack price
    public void Fixed_net_weight_pack_records_the_weighed_net_grams_and_the_charge_never_changes(int weighed)
    {
        var pack = OrderFactory.Line("PACK", 74900, 0, null, 2, false, 0, 0, "Seer Fish", "Steaks 500 g net");
        var order = OrderFactory.Place([pack]).Preparing();
        var line = order.Items[0];

        var result = order.Pack([new PackLine(line.Id, 2, "PACK", weighed * 2)], false, 0, OrderFactory.Staff, At);

        line.PackedNetGrams.Should().Be(weighed * 2);
        result[0].ChargedQuantity.Should().Be(2);
        result[0].FinalTotalPaise.Should().Be(149800);
        order.FinalTotalPaise.Should().Be(order.TotalPaise);
        order.Adjustments.Should().BeEmpty();
    }

    [Theory]
    [InlineData("KG", 500)]
    [InlineData("PACK", 0)]
    public void Packed_net_grams_are_refused_on_a_weighed_line_or_when_not_positive(string sellUnit, int grams)
    {
        var item = sellUnit == "KG" ? NoAllowanceSeerFish() : OrderFactory.Line("PACK", 74900, 0, null, 1, false, 0, 0);
        var order = OrderFactory.Place([item]).Preparing();
        var unit = sellUnit == "KG" ? "GRAM" : "PACK";
        var quantity = sellUnit == "KG" ? 500 : 1;

        var act = () => order.Pack([new PackLine(order.Items[0].Id, quantity, unit, grams)], false, 0, OrderFactory.Staff, At);

        var refusal = act.Should().Throw<RefusalException>().Which;
        refusal.Code.Should().Be("PACKED_QUANTITY_INVALID");
        refusal.Errors.Select(e => e.Field).Should().Equal("lines[0].packedNetGrams");
        order.Status.Should().Be(OrderStatus.Preparing);
    }

    private static OrderItemSnapshot NoAllowanceSeerFish()
        => OrderFactory.Line("KG", 129900, 4000, "PER_KG", 500, true, 0, 0, "Seer Fish", "Medium", "Steaks", maxOverweightBp: 0);

    [Fact]
    public void Non_weight_variable_line_packed_above_ordered_is_charged_as_ordered()
    {
        var order = OrderFactory.Place([OrderFactory.Crabs()]).Preparing();
        var line = order.Items[0];

        var result = order.Pack([new PackLine(line.Id, 3, "PIECE")], false, 0, OrderFactory.Staff, At);

        result[0].ChargedQuantity.Should().Be(2);
        result[0].FinalTotalPaise.Should().Be(130000);
        order.Adjustments.Should().BeEmpty();
    }

    [Fact]
    public void Packed_zero_short_supplies_the_line_with_a_negative_adjustment()
    {
        var order = OrderFactory.Place([OrderFactory.SeerFish(), OrderFactory.Crabs()]).Preparing();
        var fish = order.Items[0];

        var result = order.Pack([new PackLine(fish.Id, 0, "GRAM"), new PackLine(order.Items[1].Id, 2, "PIECE")], false, 0, OrderFactory.Staff, At);

        fish.Status.Should().Be(OrderItemStatus.ShortSupplied);
        result[0].Status.Should().Be(OrderItemStatus.ShortSupplied);
        result[0].FinalTotalPaise.Should().Be(0);
        order.Adjustments.Should().ContainSingle(a => a.Kind == AdjustmentKind.ShortSupply && a.AmountPaise == -66950);
        order.FinalTotalPaise.Should().Be(130000);
    }

    [Fact]
    public void Non_weight_variable_partial_pack_is_a_short_supply_adjustment()
    {
        var order = OrderFactory.Place([OrderFactory.Crabs()]).Preparing();

        order.Pack([new PackLine(order.Items[0].Id, 1, "PIECE")], false, 0, OrderFactory.Staff, At);

        order.Items[0].Status.Should().Be(OrderItemStatus.Active);
        order.Adjustments.Should().ContainSingle(a => a.Kind == AdjustmentKind.ShortSupply && a.AmountPaise == -65000);
    }

    [Fact]
    public void Prepaid_short_pack_splits_the_refund_by_reason()
    {
        var order = OrderFactory.Place([OrderFactory.SeerFish(), OrderFactory.Crabs()], PaymentMethod.Online);
        var paid = order.TotalPaise;
        order.ConfirmPayment(Guid.NewGuid(), paid, order.PlacedAt.AddMinutes(1));
        order.Preparing();

        order.Pack([new PackLine(order.Items[0].Id, 450, "GRAM"), new PackLine(order.Items[1].Id, 0, "PIECE")], false, paid, OrderFactory.Staff, At);

        // Fish 450 g = 58455 + 1800; crabs short-supplied (130000 estimated).
        order.FinalTotalPaise.Should().Be(60255);
        order.AmountDuePaise.Should().Be(0);
        order.RefundDuePaise.Should().Be(paid - 60255);
        order.RefundDueShortSupplyPaise.Should().Be(130000);
        order.RefundDueWeightShortfallPaise.Should().Be(paid - 60255 - 130000);
        var packed = order.DomainEvents.OfType<OrderPacked>().Single();
        (packed.RefundDueByReason.ShortSupply + packed.RefundDueByReason.WeightShortfall).Should().Be(packed.RefundDuePaise);
    }

    [Fact]
    public void Prepaid_overweight_is_capped_at_the_captured_amount()
    {
        var order = OrderFactory.Place([OrderFactory.SeerFish()], PaymentMethod.Online);
        var paid = order.TotalPaise;
        order.ConfirmPayment(Guid.NewGuid(), paid, order.PlacedAt.AddMinutes(1));
        order.Preparing();

        order.Pack([new PackLine(order.Items[0].Id, 540, "GRAM")], false, paid, OrderFactory.Staff, At);

        order.FinalTotalPaise.Should().Be(paid);
        order.RefundDuePaise.Should().Be(0);
        order.Items[0].FinalTotalPaise.Should().Be(72306);
        (order.Items[0].FinalTotalPaise - order.Items[0].FinalDiscountPaise).Should().Be(paid);
    }

    [Fact]
    public void Discount_is_reduced_to_the_final_eligible_subtotal()
    {
        var crab = OrderFactory.Crabs(discountPaise: 30000);
        var fish = OrderFactory.SeerFish();
        var order = OrderFactory.Place([fish, crab], discountPaise: 30000).Preparing();

        order.Pack([new PackLine(order.Items[0].Id, 500, "GRAM"), new PackLine(order.Items[1].Id, 0, "PIECE")], false, 0, OrderFactory.Staff, At);

        order.FinalDiscountPaise.Should().Be(0);
        order.FinalTotalPaise.Should().Be(66950);
    }

    [Fact]
    public void Pack_with_a_proposed_substitution_is_refused()
    {
        var order = OrderFactory.Place([OrderFactory.SeerFish(), OrderFactory.Prawns()]).Preparing();
        order.ProposeSubstitution(order.Items[0].Id, OrderFactory.Prawns(), "Prawns", 15, At);

        var act = () => order.PackedAsOrdered();

        act.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
    }

    public static TheoryData<string> InvalidPacks() => new() { "missing", "duplicate", "unit", "negative", "unknown" };

    [Theory]
    [MemberData(nameof(InvalidPacks))]
    public void Invalid_pack_lines_are_refused(string problem)
    {
        var order = OrderFactory.Place([OrderFactory.SeerFish(), OrderFactory.Crabs()]).Preparing();
        var (fish, crabs) = (order.Items[0], order.Items[1]);
        PackLine[] lines = problem switch
        {
            "missing" => [new PackLine(fish.Id, 500, "GRAM")],
            "duplicate" => [new PackLine(fish.Id, 500, "GRAM"), new PackLine(fish.Id, 500, "GRAM"), new PackLine(crabs.Id, 2, "PIECE")],
            "unit" => [new PackLine(fish.Id, 500, "PIECE"), new PackLine(crabs.Id, 2, "PIECE")],
            "negative" => [new PackLine(fish.Id, -1, "GRAM"), new PackLine(crabs.Id, 2, "PIECE")],
            _ => [new PackLine(Guid.NewGuid(), 1, "GRAM"), new PackLine(fish.Id, 500, "GRAM"), new PackLine(crabs.Id, 2, "PIECE")],
        };

        var act = () => order.Pack(lines, false, 0, OrderFactory.Staff, At);

        act.Should().Throw<RefusalException>().Which.Code.Should().Be("PACKED_QUANTITY_INVALID");
        order.Status.Should().Be(OrderStatus.Preparing);
    }

    [Fact]
    public void Pack_outside_preparing_is_an_invalid_transition()
    {
        var order = OrderFactory.ApiExample();

        var act = () => order.PackedAsOrdered();

        act.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
    }

    [Fact]
    public void Goodwill_reduces_cod_amount_due_and_cannot_exceed_the_final_total()
    {
        var order = OrderFactory.ApiExample().Preparing().PackedAsOrdered();
        var due = order.AmountDuePaise;

        order.AddGoodwill(-5000, "Late delivery", null, Guid.NewGuid(), alreadyPaid: false, At);

        order.AmountDuePaise.Should().Be(due - 5000);
        order.DomainEvents.OfType<OrderAdjusted>().Should().ContainSingle().Which.Kind.Should().Be("GOODWILL");
        var tooMuch = () => order.AddGoodwill(-(order.FinalTotalPaise!.Value), "Everything", null, null, false, At);
        tooMuch.Should().Throw<DomainException>().Which.Code.Should().Be("GOODWILL_EXCEEDS_TOTAL");
    }
}
