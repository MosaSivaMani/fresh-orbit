using AwesomeAssertions;
using FreshOrbit.Inventory.Domain.ColdChain;
using FreshOrbit.Inventory.Domain.ColdChain.Events;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Domain.Stock.Events;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Inventory.Public.Events;
using FreshOrbit.SharedKernel.Errors;
using DomainStockUnit = FreshOrbit.Inventory.Domain.Stock.StockUnit;

namespace FreshOrbit.Inventory.Tests.Unit;

public sealed class StockItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 4, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(500, 6500, 770)]
    [InlineData(1000, 10000, 1000)]
    [InlineData(1, 3333, 4)]
    public void U1_U2_gross_rounds_up_by_yield(int net, int yieldBp, int expected)
    {
        StockQuantities.Gross(DomainStockUnit.Gram, net, yieldBp).Should().Be(expected);
        GrossQuantity.From(QuantityUnit.Gram, net, yieldBp).Should().Be(expected);
    }

    [Fact]
    public void U3_gross_for_pieces_ignores_yield()
    {
        StockQuantities.Gross(DomainStockUnit.Piece, 2, 6500).Should().Be(2);
        GrossQuantity.From(QuantityUnit.Piece, 2, 6500).Should().Be(2);
    }

    [Fact]
    public void U4_max_net_orderable_is_the_exact_inverse_of_gross()
    {
        var maxNet = StockQuantities.MaxNetOrderable(DomainStockUnit.Gram, 770, 6500);

        maxNet.Should().Be(500);
        StockQuantities.Gross(DomainStockUnit.Gram, maxNet, 6500).Should().BeLessThanOrEqualTo(770);
        GrossQuantity.MaxNet(QuantityUnit.Gram, 770, 6500).Should().Be(500);
    }

    [Fact]
    public void Yield_outside_1_to_10000_is_refused()
    {
        var act = () => StockQuantities.Gross(DomainStockUnit.Gram, 500, 0);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void U5_reserve_more_than_available_is_refused_without_change()
    {
        var item = Item(Batch(1000, 845_000, days: 2));
        item.Reserve(Guid.NewGuid(), Guid.NewGuid(), 300, null);

        var act = () => item.Reserve(Guid.NewGuid(), Guid.NewGuid(), 770, null);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("OUT_OF_STOCK");
        item.Reserved.Should().Be(300);
        item.Reservations.Should().HaveCount(1);
    }

    [Fact]
    public void U7_FEFO_takes_the_earliest_best_before_first()
    {
        var item = Item(Batch(1000, 100_000, days: 2, code: "B"), Batch(300, 30_000, days: 1, code: "A"));
        var orderItemId = Guid.NewGuid();
        item.Reserve(Guid.NewGuid(), orderItemId, 770, null);

        item.ConsumeForPack(orderItemId, 770, saleAlreadyRecorded: false, Now).Should().BeTrue();

        var a = item.Batches.Single(b => b.BatchCode == "A");
        var b = item.Batches.Single(b => b.BatchCode == "B");
        Sales(item).Should().Equal((a.Id, -300), (b.Id, -470));
        a.Status.Should().Be(BatchStatus.Depleted);
        item.OnHand.Should().Be(530);
        item.Reserved.Should().Be(0);
        item.Reservations.Single().Status.Should().Be(ReservationStatus.Consumed);
    }

    [Fact]
    public void U8_FEFO_tie_on_best_before_takes_the_older_receipt_first()
    {
        var item = StockItem.Create(Guid.NewGuid(), Guid.NewGuid(), DomainStockUnit.Gram, 0);
        var bestBefore = Now.AddDays(2);
        item.Receive(new BatchReceipt("OLD", null, null, null, 200, 20_000, bestBefore, null), Now.AddHours(-2), null, false);
        item.Receive(new BatchReceipt("NEW", null, null, null, 200, 20_000, bestBefore, null), Now.AddHours(-1), null, false);

        item.RecordWaste(WasteReason.Damaged, null, 250, null, Guid.NewGuid(), Now, null);

        var old = item.Batches.Single(b => b.BatchCode == "OLD");
        old.RemainingQuantity.Should().Be(0);
        item.Batches.Single(b => b.BatchCode == "NEW").RemainingQuantity.Should().Be(150);
    }

    [Fact]
    public void U9_waste_cost_is_proportional_and_rounded_half_away_from_zero()
    {
        var item = Item(Batch(10_000, 845_000, days: 2));

        var movement = item.RecordWaste(WasteReason.Spoiled, null, 333, null, Guid.NewGuid(), Now, null).Single();

        movement.Quantity.Should().Be(-333);
        movement.CostPaise.Should().Be(-28_139);
        var wasted = item.DomainEvents.OfType<WasteRecorded>().Single();
        wasted.TotalCostPaise.Should().Be(-28_139);
        wasted.ReasonCode.Should().Be("SPOILED");
    }

    [Fact]
    public void U10_draining_a_batch_makes_its_movement_costs_sum_to_the_total_cost()
    {
        var item = Item(Batch(1000, 100_001, days: 2));
        var receiptCost = item.Movements.Single().CostPaise;

        item.RecordWaste(WasteReason.Damaged, null, 333, null, Guid.NewGuid(), Now, null);
        item.RecordWaste(WasteReason.Damaged, null, 333, null, Guid.NewGuid(), Now, null);
        item.RecordWaste(WasteReason.Damaged, null, 334, null, Guid.NewGuid(), Now, null);

        item.Movements.Sum(m => m.CostPaise).Should().Be(0);
        item.Movements.Where(m => m.Kind == MovementKind.Waste).Sum(m => m.CostPaise).Should().Be(-receiptCost);
        item.Batches.Single().RemainingCostPaise.Should().Be(0);
    }

    [Fact]
    public void U11_commands_naming_a_discarded_batch_are_refused()
    {
        var item = Item(Batch(1000, 100_000, days: 2), Batch(500, 50_000, days: 3));
        var batchId = item.Batches[0].Id;
        item.DiscardBatch(batchId, WasteReason.QcRejected, null, Now, null);

        var waste = () => item.RecordWaste(WasteReason.Damaged, null, 10, batchId, Guid.NewGuid(), Now, null);
        var adjust = () => item.Adjust(AdjustmentKind.AdjustmentIn, AdjustmentReason.Found, null, 10, batchId, Guid.NewGuid(), Now, null);

        waste.Should().Throw<DomainException>().Which.Code.Should().Be("BATCH_NOT_ACTIVE");
        adjust.Should().Throw<DomainException>().Which.Code.Should().Be("BATCH_NOT_ACTIVE");
    }

    [Fact]
    public void U12_adjustment_out_below_reserved_is_refused()
    {
        var item = Item(Batch(1000, 100_000, days: 2));
        item.Reserve(Guid.NewGuid(), Guid.NewGuid(), 800, null);

        var act = () => item.Adjust(AdjustmentKind.AdjustmentOut, AdjustmentReason.Lost, null, 300, null, Guid.NewGuid(), Now, null);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ADJUSTMENT_BELOW_ZERO");
        item.OnHand.Should().Be(1000);
    }

    [Fact]
    public void U13_waste_below_reserved_is_recorded_and_raises_StockOverCommitted()
    {
        var item = Item(Batch(1000, 100_000, days: 2));
        item.Reserve(Guid.NewGuid(), Guid.NewGuid(), 800, null);
        item.ClearEvents();

        item.RecordWaste(WasteReason.Spoiled, null, 300, null, Guid.NewGuid(), Now, null);

        item.OnHand.Should().Be(700);
        item.DomainEvents.OfType<StockOverCommitted>().Single().Shortfall.Should().Be(100);
    }

    [Fact]
    public void U14_packing_more_than_the_batches_hold_writes_a_batchless_pair()
    {
        var item = Item(Batch(500, 50_000, days: 2));
        var orderItemId = Guid.NewGuid();
        item.Reserve(Guid.NewGuid(), orderItemId, 500, null);

        item.ConsumeForPack(orderItemId, 580, saleAlreadyRecorded: false, Now);

        var batchless = item.Movements.Where(m => m.BatchId is null).ToList();
        batchless.Select(m => (m.Kind, m.Quantity, m.CostPaise, m.ReasonCode)).Should().BeEquivalentTo(
            [(MovementKind.CountCorrection, 80, 0L, "PACK_SHORTFALL"), (MovementKind.Sale, -80, 0L, "PACK_SHORTFALL")]);
        item.OnHand.Should().Be(item.Batches.Where(b => b.IsActive).Sum(b => b.RemainingQuantity)).And.Be(0);
        item.DomainEvents.OfType<StockDiscrepancyFound>().Single().GapQuantity.Should().Be(80);
    }

    [Fact]
    public void U15_low_stock_events_fire_on_the_crossing_only()
    {
        var item = Item(Batch(1200, 120_000, days: 2));
        item.SetLowStockThreshold(1000);
        item.ClearEvents();

        item.RecordWaste(WasteReason.Damaged, null, 300, null, Guid.NewGuid(), Now, null);
        item.RecordWaste(WasteReason.Damaged, null, 100, null, Guid.NewGuid(), Now, null);
        item.Adjust(AdjustmentKind.AdjustmentIn, AdjustmentReason.Found, null, 300, null, Guid.NewGuid(), Now, null);

        item.DomainEvents.OfType<LowStockReached>().Should().ContainSingle().Which.Available.Should().Be(900);
        item.DomainEvents.OfType<StockReplenished>().Should().ContainSingle().Which.Available.Should().Be(1100);
    }

    [Fact]
    public void U16_threshold_zero_raises_no_low_stock_but_out_of_stock_at_zero()
    {
        var item = Item(Batch(500, 50_000, days: 2));
        item.ClearEvents();

        item.RecordWaste(WasteReason.Damaged, null, 500, null, Guid.NewGuid(), Now, null);

        item.DomainEvents.OfType<LowStockReached>().Should().BeEmpty();
        item.DomainEvents.OfType<OutOfStockReached>().Should().ContainSingle();
    }

    [Fact]
    public void Stock_available_again_fires_only_when_available_goes_from_zero_to_above_zero()
    {
        var item = StockItem.Create(Guid.NewGuid(), Guid.NewGuid(), DomainStockUnit.Gram, 0);
        item.ClearEvents();

        item.Receive(new BatchReceipt("FIRST", null, null, null, 500, 50_000, Now.AddDays(2), null), Now, null, false);
        item.Receive(new BatchReceipt("SECOND", null, null, null, 300, 30_000, Now.AddDays(2), null), Now, null, false);

        var again = item.DomainEvents.OfType<StockAvailableAgain>().Should().ContainSingle().Which;
        (again.StockItemId, again.FulfillmentLocationId, again.VariantId, again.Available)
            .Should().Be((item.Id, item.FulfillmentLocationId, item.VariantId, 500));
    }

    [Fact]
    public void Stock_available_again_fires_when_a_released_reservation_frees_sold_out_stock()
    {
        var item = Item(Batch(500, 50_000, days: 2));
        var orderId = Guid.NewGuid();
        item.Reserve(orderId, Guid.NewGuid(), 500, null);
        item.ClearEvents();

        item.CloseReservationsOfOrder(orderId, ReservationStatus.Released);

        item.DomainEvents.OfType<StockAvailableAgain>().Should().ContainSingle().Which.Available.Should().Be(500);
    }

    [Fact]
    public void Stock_available_again_does_not_fire_while_stock_stays_above_zero_or_stays_at_zero()
    {
        var item = Item(Batch(500, 50_000, days: 2));
        item.ClearEvents();

        item.Adjust(AdjustmentKind.AdjustmentIn, AdjustmentReason.Found, null, 100, null, Guid.NewGuid(), Now, null);
        item.RecordWaste(WasteReason.Damaged, null, 600, null, Guid.NewGuid(), Now, null);
        item.Count(0, null, null, Guid.NewGuid(), Now, null);

        item.DomainEvents.OfType<StockAvailableAgain>().Should().BeEmpty();
    }

    [Fact]
    public void U17_count_equal_to_on_hand_writes_nothing()
    {
        var item = Item(Batch(500, 50_000, days: 2));
        item.ClearEvents();

        item.Count(500, null, null, Guid.NewGuid(), Now, null).Should().BeEmpty();
        item.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void U18_positive_count_without_active_batch_or_batch_id_is_refused()
    {
        var item = StockItem.Create(Guid.NewGuid(), Guid.NewGuid(), DomainStockUnit.Gram, 0);

        var act = () => item.Count(100, null, null, Guid.NewGuid(), Now, null);

        act.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "batchId" && e.Code == "VALIDATION_FAILED");
    }

    [Fact]
    public void Count_below_on_hand_corrects_FEFO_and_above_goes_into_the_newest_batch()
    {
        var item = Item(Batch(300, 30_000, days: 1, code: "A"), Batch(300, 30_000, days: 2, code: "B"));

        item.Count(400, null, null, Guid.NewGuid(), Now, null);
        item.Batches.Single(b => b.BatchCode == "A").RemainingQuantity.Should().Be(100);
        item.Count(450, null, null, Guid.NewGuid(), Now, null);

        item.Batches.Single(b => b.BatchCode == "B").RemainingQuantity.Should().Be(350);
        item.OnHand.Should().Be(450);
        item.DomainEvents.OfType<StockAdjusted>().Select(e => e.Quantity).Should().Equal(-200, 50);
    }

    [Fact]
    public void Adjustment_in_reactivates_a_depleted_batch_named_explicitly()
    {
        var item = Item(Batch(100, 10_000, days: 2));
        var batch = item.Batches.Single();
        item.RecordWaste(WasteReason.Damaged, null, 100, null, Guid.NewGuid(), Now, null);
        batch.Status.Should().Be(BatchStatus.Depleted);

        item.Adjust(AdjustmentKind.AdjustmentIn, AdjustmentReason.Found, null, 40, batch.Id, Guid.NewGuid(), Now, null);

        batch.Status.Should().Be(BatchStatus.Active);
        batch.RemainingQuantity.Should().Be(40);
        item.OnHand.Should().Be(40);
    }

    [Fact]
    public void Adjustment_reason_OTHER_needs_notes()
    {
        var item = Item(Batch(100, 10_000, days: 2));

        var act = () => item.Adjust(AdjustmentKind.AdjustmentIn, AdjustmentReason.Other, " ", 10, null, Guid.NewGuid(), Now, null);

        act.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "notes");
    }

    [Fact]
    public void Expiring_batches_write_off_the_remainder_and_raise_BatchExpired()
    {
        var item = Item(Batch(400, 40_000, days: 1), Batch(500, 50_000, days: 3));
        item.ClearEvents();

        var expired = item.ExpireDueBatches(Now.AddDays(1));

        expired.Should().ContainSingle().Which.Status.Should().Be(BatchStatus.Expired);
        item.OnHand.Should().Be(500);
        var waste = item.Movements.Single(m => m.Kind == MovementKind.Waste);
        (waste.Quantity, waste.CostPaise, waste.ReasonCode).Should().Be((-400, -40_000L, "EXPIRED"));
        item.DomainEvents.OfType<BatchExpired>().Single().WrittenOffQuantity.Should().Be(400);
    }

    [Fact]
    public void Releasing_and_expiring_reservations_restore_reserved_once()
    {
        var item = Item(Batch(1000, 100_000, days: 2));
        var orderId = Guid.NewGuid();
        item.Reserve(orderId, Guid.NewGuid(), 300, Now.AddMinutes(15));
        item.Reserve(orderId, Guid.NewGuid(), 200, Now.AddMinutes(15));

        item.ClearReservationExpiry(orderId).Should().Be(2);
        item.CloseReservationsOfOrder(orderId, ReservationStatus.Released).Should().Be(2);
        item.CloseReservationsOfOrder(orderId, ReservationStatus.Released).Should().Be(0);

        item.Reserved.Should().Be(0);
        item.Reservations.Should().OnlyContain(r => r.Status == ReservationStatus.Released && r.ExpiresAt == null);
    }

    [Fact]
    public void Pack_of_an_already_consumed_line_is_skipped()
    {
        var item = Item(Batch(1000, 100_000, days: 2));

        item.ConsumeForPack(Guid.NewGuid(), 500, saleAlreadyRecorded: true, Now).Should().BeFalse();
        item.OnHand.Should().Be(1000);
    }

    [Theory]
    [InlineData("CHILLER", 4.1)]
    [InlineData("FREEZER", -17.5)]
    public void U19_cold_chain_out_of_range_readings_are_flagged(string unit, double temperature)
    {
        var range = unit == "CHILLER" ? new StorageUnitRange("CHILLER", -1.0m, 4.0m, "Chiller") : new StorageUnitRange("FREEZER", null, -18.0m, "Freezer");

        var log = ColdChainLog.Record(Guid.NewGuid(), range, (decimal)temperature, Now, null);

        log.IsOutOfRange.Should().BeTrue();
        log.DomainEvents.OfType<ColdChainBreachRecorded>().Should().ContainSingle();
    }

    [Fact]
    public void Cold_chain_reading_inside_the_range_is_not_flagged()
    {
        var log = ColdChainLog.Record(Guid.NewGuid(), new StorageUnitRange("CHILLER", -1.0m, 4.0m, "Chiller"), 4.0m, Now, null);

        log.IsOutOfRange.Should().BeFalse();
        log.DomainEvents.Should().BeEmpty();
    }

    private static (int Days, int Quantity, long Cost, string? Code) Batch(int quantity, long cost, int days, string? code = null) => (days, quantity, cost, code);

    private static StockItem Item(params (int Days, int Quantity, long Cost, string? Code)[] batches)
    {
        var item = StockItem.Create(Guid.NewGuid(), Guid.NewGuid(), DomainStockUnit.Gram, 0);
        var receivedAt = Now.AddHours(-1);
        foreach (var (days, quantity, cost, code) in batches)
        {
            receivedAt = receivedAt.AddMinutes(1);
            item.Receive(new BatchReceipt(code ?? Guid.NewGuid().ToString("N")[..10], null, null, null, quantity, cost, Now.AddDays(days), null), receivedAt, null, false);
        }

        return item;
    }

    private static List<(Guid BatchId, int Quantity)> Sales(StockItem item)
        => [.. item.Movements.Where(m => m.Kind == MovementKind.Sale).Select(m => (m.BatchId!.Value, m.Quantity))];
}
