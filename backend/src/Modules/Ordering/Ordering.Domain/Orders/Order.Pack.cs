using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Pricing;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Ordering.Domain.Orders;

public sealed partial class Order
{
    /// <summary>
    /// Weigh and pack every ACTIVE line (ORD-R25..R31): validates the lines, computes final line amounts with the
    /// shared line formula at <c>min(packed, maxChargeable)</c>, writes WEIGHT / SHORT_SUPPLY adjustments, re-allocates
    /// the coupon discount, caps a prepaid total at the captured amount (D15) and raises OrderPacked.
    /// </summary>
    /// <param name="paidPaise">Captured amount for ONLINE orders (Payments); ignored for COD / UPI on delivery.</param>
    public IReadOnlyList<PackedLineResult> Pack(IReadOnlyList<PackLine> lines, bool coldBoxUsed, long paidPaise, OrderActor actor, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(actor);
        if (Status != OrderStatus.Preparing)
        {
            throw InvalidTransition(OrderStatus.Packed);
        }

        if (_substitutions.Exists(s => s.Status == SubstitutionStatus.Proposed))
        {
            throw new ConflictException(OrderingErrors.InvalidStatusTransition, "A substitution is waiting for the customer's answer; pack after it is answered or expired.");
        }

        var packedById = ValidatePackLines(lines);
        var adjustmentByItem = new Dictionary<Guid, long>();
        foreach (var item in _items)
        {
            if (item.Status != OrderItemStatus.Active)
            {
                item.SetZeroFinal();
                continue;
            }

            var packed = packedById[item.Id];
            if (packed == 0)
            {
                item.SetFinal(0, 0, default);
                item.MarkStatus(OrderItemStatus.ShortSupplied);
                AddAdjustment(item.Id, AdjustmentKind.ShortSupply, -item.EstimatedTotalPaise, "Not packed", actor.Id, at, adjustmentByItem);
                continue;
            }

            var charged = LinePricing.ChargedQuantity(packed, item.MaxChargeableQuantity);
            var amounts = LinePricing.Compute(item.Facts, charged);
            item.SetFinal(packed, charged, amounts);
            var difference = amounts.TotalPaise - item.EstimatedTotalPaise;
            if (!item.IsWeightVariable && packed < item.OrderedQuantity)
            {
                AddAdjustment(item.Id, AdjustmentKind.ShortSupply, difference, "Packed below the ordered quantity", actor.Id, at, adjustmentByItem);
            }
            else if (item.IsWeightVariable && difference != 0)
            {
                AddAdjustment(item.Id, AdjustmentKind.Weight, difference, "Actual net weight", actor.Id, at, adjustmentByItem);
            }
        }

        ApplyFinalTotals(paidPaise);
        ColdBoxUsed = coldBoxUsed;
        ChangeStatus(OrderStatus.Packed, actor, null, at);
        Raise(new OrderPacked(
            Id,
            OrderNumber,
            CustomerId,
            FulfillmentLocationId,
            at,
            coldBoxUsed,
            PaymentMethod.ToCode(),
            FinalTotalPaise ?? 0,
            AmountDuePaise,
            RefundDuePaise,
            new RefundDueByReason(RefundDueWeightShortfallPaise, RefundDueShortSupplyPaise),
            [.. _items.Select(i => new OrderPackedLine(
                i.Id,
                i.VariantId,
                i.StockUnit,
                i.YieldBp,
                i.OrderedQuantity,
                i.PackedQuantity ?? 0,
                i.ChargedQuantity ?? 0,
                i.FinalTotalPaise ?? 0,
                i.Status.ToCode()))]));

        return [.. _items.Where(i => packedById.ContainsKey(i.Id)).Select(i => new PackedLineResult(
            i.Id,
            i.PackedQuantity ?? 0,
            i.ChargedQuantity ?? 0,
            i.QuantityUnit,
            (i.PackedQuantity ?? 0) > i.MaxChargeableQuantity,
            i.FinalTotalPaise ?? 0,
            adjustmentByItem.GetValueOrDefault(i.Id),
            i.Status))];
    }

    /// <summary>Total goodwill already given (negative amounts summed as a positive number).</summary>
    public long GoodwillGivenPaise => -Money.Sum(_adjustments.Where(a => a.Kind == AdjustmentKind.Goodwill).Select(a => a.AmountPaise));

    /// <summary>
    /// Goodwill adjustment (ORD-R58): negative amount, PACKED..DELIVERED, Σ goodwill ≤ final total. Before collection of
    /// a pay-on-delivery order it reduces the amount due; otherwise it becomes refund due. Raises OrderAdjusted.
    /// </summary>
    public OrderAdjustment AddGoodwill(long amountPaise, string reason, Guid? orderItemId, Guid? requestedBy, bool alreadyPaid, DateTimeOffset at)
    {
        if (Status is not (OrderStatus.Packed or OrderStatus.OutForDelivery or OrderStatus.Delivered))
        {
            throw new ConflictException(OrderingErrors.InvalidStatusTransition, "Goodwill can be given only once the order is packed and until it is delivered.");
        }

        var errors = new List<FieldError>();
        if (amountPaise >= 0)
        {
            errors.Add(new FieldError("amountPaise", OrderingErrors.ValidationFailed, "Goodwill must be a negative amount in paise."));
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 200)
        {
            errors.Add(new FieldError("reason", OrderingErrors.ValidationFailed, "Give a reason of up to 200 characters."));
        }

        if (orderItemId is { } itemId && FindItem(itemId) is null)
        {
            errors.Add(new FieldError("orderItemId", OrderingErrors.ValidationFailed, "The line does not belong to this order."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        if (GoodwillGivenPaise - amountPaise > (FinalTotalPaise ?? TotalPaise))
        {
            throw new DomainException(OrderingErrors.GoodwillExceedsTotal, "Total goodwill cannot exceed the order's final total.", "amountPaise");
        }

        if (!IsPrepaid && !alreadyPaid)
        {
            AmountDuePaise = Math.Max(0, AmountDuePaise + amountPaise);
        }
        else
        {
            RefundDuePaise -= amountPaise;
        }

        var adjustment = new OrderAdjustment(Id, orderItemId, AdjustmentKind.Goodwill, amountPaise, reason.Trim(), requestedBy, at);
        _adjustments.Add(adjustment);
        Touch(at);
        Raise(new OrderAdjusted(Id, OrderNumber, CustomerId, FulfillmentLocationId, adjustment.Id, AdjustmentKind.Goodwill.ToCode(), amountPaise, AmountDuePaise, RefundDuePaise, adjustment.Reason, requestedBy, at));
        return adjustment;
    }

    private Dictionary<Guid, int> ValidatePackLines(IReadOnlyList<PackLine> lines)
    {
        var errors = new List<FieldError>();
        var packed = new Dictionary<Guid, int>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var field = $"lines[{i}]";
            var item = line is null ? null : FindItem(line.OrderItemId);
            if (line is null || item is null || item.Status != OrderItemStatus.Active)
            {
                errors.Add(new FieldError(field, OrderingErrors.PackedQuantityInvalid, "The line is not an active line of this order."));
            }
            else if (packed.ContainsKey(item.Id))
            {
                errors.Add(new FieldError(field, OrderingErrors.PackedQuantityInvalid, "Each line can be packed only once."));
            }
            else if (!string.Equals(line.Unit, item.QuantityUnit, StringComparison.Ordinal))
            {
                errors.Add(new FieldError(field, OrderingErrors.PackedQuantityInvalid, $"Send the packed quantity in {item.QuantityUnit}."));
            }
            else if (line.PackedQuantity < 0)
            {
                errors.Add(new FieldError(field, OrderingErrors.PackedQuantityInvalid, "The packed quantity cannot be negative."));
            }
            else
            {
                packed[item.Id] = line.PackedQuantity;
            }
        }

        if (errors.Count == 0 && _items.Exists(i => i.Status == OrderItemStatus.Active && !packed.ContainsKey(i.Id)))
        {
            errors.Add(new FieldError("lines", OrderingErrors.PackedQuantityInvalid, "Every active line must be packed; send 0 for a line that is not available."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(OrderingErrors.PackedQuantityInvalid, "Some packed quantities are invalid.", errors);
        }

        return packed;
    }

    /// <summary>ORD-R30, R31: discount re-allocation, prepaid cap (D15), final split, amount due and refund due.</summary>
    private void ApplyFinalTotals(long paidPaise)
    {
        var active = _items.Where(i => i.Status == OrderItemStatus.Active).ToList();
        var eligible = active.Where(i => i.DiscountPaise > 0).ToList();
        var finalEligible = Money.Sum(eligible.Select(i => i.FinalTotalPaise ?? 0));
        var couponDiscount = Math.Min(DiscountPaise, finalEligible);
        var lineDiscount = active.ToDictionary(i => i.Id, _ => 0L);
        var couponParts = Money.Allocate(couponDiscount, [.. eligible.Select(i => i.FinalTotalPaise ?? 0)]);
        for (var i = 0; i < eligible.Count; i++)
        {
            lineDiscount[eligible[i].Id] = couponParts[i];
        }

        var linesTotal = Money.Sum(active.Select(i => i.FinalTotalPaise ?? 0));
        var computedTotal = linesTotal - couponDiscount + DeliveryFeePaise;
        long capWaiver = 0;
        if (IsPrepaid && computedTotal > paidPaise)
        {
            // D15: a prepaid order never costs more than was captured; the excess is waived on the lines.
            capWaiver = Math.Min(computedTotal - Math.Max(0, paidPaise), linesTotal - couponDiscount);
            var capParts = Money.Allocate(capWaiver, [.. active.Select(i => (i.FinalTotalPaise ?? 0) - lineDiscount[i.Id])]);
            for (var i = 0; i < active.Count; i++)
            {
                lineDiscount[active[i].Id] += capParts[i];
            }
        }

        foreach (var item in active)
        {
            item.SetFinalDiscountAndTax(lineDiscount[item.Id]);
        }

        FinalItemsPaise = Money.Sum(active.Select(i => i.FinalItemPaise ?? 0));
        FinalPreparationPaise = Money.Sum(active.Select(i => i.FinalPreparationPaise ?? 0));
        FinalDiscountPaise = couponDiscount + capWaiver;
        FinalTaxPaise = Money.Sum(active.Select(i => i.FinalTaxPaise ?? 0)) + LinePricing.ContainedTax(DeliveryFeePaise, DeliveryFeeTaxRateBp);
        FinalTotalPaise = linesTotal - FinalDiscountPaise + DeliveryFeePaise;
        if (IsPrepaid)
        {
            AmountDuePaise = 0;
            RefundDuePaise = Math.Max(0, paidPaise - FinalTotalPaise.Value);
            var shortSupplied = Money.Sum(_items.Where(i => i.Status == OrderItemStatus.ShortSupplied).Select(i => i.EstimatedTotalPaise));
            RefundDueShortSupplyPaise = Math.Min(RefundDuePaise, shortSupplied);
            RefundDueWeightShortfallPaise = RefundDuePaise - RefundDueShortSupplyPaise;
        }
        else
        {
            AmountDuePaise = FinalTotalPaise.Value;
            RefundDuePaise = RefundDueShortSupplyPaise = RefundDueWeightShortfallPaise = 0;
        }
    }

    private void AddAdjustment(Guid? orderItemId, AdjustmentKind kind, long amountPaise, string reason, Guid? createdBy, DateTimeOffset at, Dictionary<Guid, long>? byItem = null)
    {
        if (amountPaise == 0)
        {
            return;
        }

        _adjustments.Add(new OrderAdjustment(Id, orderItemId, kind, amountPaise, reason, createdBy, at));
        if (byItem is not null && orderItemId is { } itemId)
        {
            byItem[itemId] = byItem.GetValueOrDefault(itemId) + amountPaise;
        }
    }
}
