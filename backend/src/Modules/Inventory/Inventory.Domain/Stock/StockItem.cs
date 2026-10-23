using FreshOrbit.Inventory.Domain.Shared;
using FreshOrbit.Inventory.Domain.Stock.Events;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Inventory.Domain.Stock;

/// <summary>Fields of a stock receipt (INV-R4).</summary>
public sealed record BatchReceipt(
    string BatchCode,
    string? SupplierName,
    string? SourceLandingCenter,
    DateOnly? CaughtOrHarvestedOn,
    int Quantity,
    long TotalCostPaise,
    DateTimeOffset BestBeforeAt,
    decimal? ReceiptTemperatureC);

/// <summary>
/// Stock of one variant at one fulfillment location. Decides availability, reservations, consumption, FEFO picking,
/// movement cost and batch state. <see cref="OnHand"/> = Σ remaining of ACTIVE batches (INV-I1) and
/// <see cref="Reserved"/> = Σ ACTIVE reservations (INV-I2); every change of <see cref="OnHand"/> comes with a movement (INV-I5).
/// <para>
/// Loaded children: ACTIVE batches (plus an explicitly named batch) and ACTIVE reservations. <see cref="Movements"/>
/// holds only the movements written by the current command (the ledger is insert-only and never loaded).
/// </para>
/// </summary>
public sealed class StockItem : AggregateRoot
{
    private readonly List<Batch> _batches = [];
    private readonly List<StockMovement> _movements = [];
    private readonly List<StockReservation> _reservations = [];

    private StockItem()
    {
    }

    private StockItem(Guid fulfillmentLocationId, Guid variantId, StockUnit stockUnit, int lowStockThreshold)
    {
        FulfillmentLocationId = fulfillmentLocationId;
        VariantId = variantId;
        StockUnit = stockUnit;
        LowStockThreshold = lowStockThreshold;
    }

    public Guid FulfillmentLocationId { get; private set; }

    public Guid VariantId { get; private set; }

    public StockUnit StockUnit { get; private set; }

    public int OnHand { get; private set; }

    public int Reserved { get; private set; }

    public int LowStockThreshold { get; private set; }

    /// <summary>On hand minus reserved; may be negative after a physical fact (D7).</summary>
    public int Available => OnHand - Reserved;

    public IReadOnlyList<Batch> Batches => _batches;

    /// <summary>Movements written by the current command.</summary>
    public IReadOnlyList<StockMovement> Movements => _movements;

    public IReadOnlyList<StockReservation> Reservations => _reservations;

    public static StockItem Create(Guid fulfillmentLocationId, Guid variantId, StockUnit stockUnit, int lowStockThreshold)
    {
        EnsureThreshold(lowStockThreshold);
        return new StockItem(fulfillmentLocationId, variantId, stockUnit, lowStockThreshold);
    }

    public void SetLowStockThreshold(int lowStockThreshold)
    {
        EnsureThreshold(lowStockThreshold);
        var before = Snapshot();
        LowStockThreshold = lowStockThreshold;
        RaiseLevelChanges(before);
    }

    /// <summary>Receives a new ACTIVE batch with a RECEIPT movement costing the batch total (INV-R4, R15).</summary>
    public Batch Receive(BatchReceipt receipt, DateTimeOffset now, Guid? recordedBy, bool temperatureWarning)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.Quantity <= 0)
        {
            throw Invalid("receivedQuantity.value", "Received quantity must be greater than zero.");
        }

        if (receipt.TotalCostPaise < 0)
        {
            throw Invalid("totalCostPaise", "Total cost must not be negative.");
        }

        if (receipt.BestBeforeAt <= now)
        {
            throw Invalid("bestBeforeAt", "Best-before time must be in the future.");
        }

        var before = Snapshot();
        var batch = new Batch(
            this,
            receipt.BatchCode,
            receipt.SupplierName,
            receipt.SourceLandingCenter,
            receipt.CaughtOrHarvestedOn,
            now,
            receipt.Quantity,
            receipt.BestBeforeAt,
            receipt.ReceiptTemperatureC,
            receipt.TotalCostPaise);
        _batches.Add(batch);
        OnHand += receipt.Quantity;
        _movements.Add(new StockMovement(Id, batch.Id, MovementKind.Receipt, receipt.Quantity, receipt.TotalCostPaise, null, null, MovementReferenceType.Batch, batch.Id, now, recordedBy));
        Raise(new StockReceived(Id, FulfillmentLocationId, VariantId, batch.Id, batch.BatchCode, receipt.Quantity, receipt.TotalCostPaise, receipt.BestBeforeAt, temperatureWarning));
        RaiseLevelChanges(before);
        return batch;
    }

    /// <summary>Records waste from a named batch or FEFO (INV-R5, R6, R14, R15). May over-commit (D7).</summary>
    public IReadOnlyList<StockMovement> RecordWaste(WasteReason reason, string? notes, int quantity, Guid? batchId, Guid referenceId, DateTimeOffset now, Guid? recordedBy)
    {
        EnsurePositive(quantity, "quantity");
        var before = Snapshot();
        var reasonCode = reason.ToCode();
        List<StockMovement> movements;
        if (batchId is { } id)
        {
            var batch = RequireBatch(id, forIncrease: false, now);
            if (batch.RemainingQuantity < quantity)
            {
                throw Invalid("quantity", "Quantity is more than the batch holds.");
            }

            movements = [Post(MovementKind.Waste, batch, -quantity, reasonCode, notes, MovementReferenceType.Manual, referenceId, now, recordedBy)];
        }
        else
        {
            if (ActiveBatchQuantity < quantity)
            {
                throw Invalid("quantity", "Quantity is more than the active batches hold.");
            }

            movements = TakeFefo(MovementKind.Waste, quantity, reasonCode, notes, MovementReferenceType.Manual, referenceId, now, recordedBy, out _);
        }

        RaiseWaste(reasonCode, movements);
        RaiseLevelChanges(before);
        return movements;
    }

    /// <summary>Writes off the whole remaining quantity of an ACTIVE batch and marks it DISCARDED.</summary>
    public IReadOnlyList<StockMovement> DiscardBatch(Guid batchId, WasteReason reason, string? notes, DateTimeOffset now, Guid? recordedBy)
    {
        var batch = RequireBatch(batchId, forIncrease: false, now);
        var before = Snapshot();
        var reasonCode = reason.ToCode();
        List<StockMovement> movements = batch.RemainingQuantity > 0
            ? [Post(MovementKind.Waste, batch, -batch.RemainingQuantity, reasonCode, notes, MovementReferenceType.Batch, batch.Id, now, recordedBy)]
            : [];
        batch.MarkDiscarded();
        RaiseWaste(reasonCode, movements);
        RaiseLevelChanges(before);
        return movements;
    }

    /// <summary>ADJUSTMENT_IN / RETURN into a batch (INV-R8) or ADJUSTMENT_OUT (INV-R7, never over-commits).</summary>
    public IReadOnlyList<StockMovement> Adjust(AdjustmentKind kind, AdjustmentReason reason, string? notes, int quantity, Guid? batchId, Guid referenceId, DateTimeOffset now, Guid? recordedBy)
    {
        EnsurePositive(quantity, "quantity");
        if (reason == AdjustmentReason.Other && string.IsNullOrWhiteSpace(notes))
        {
            throw Invalid("notes", "Notes are required when the reason is OTHER.");
        }

        var before = Snapshot();
        var reasonCode = reason.ToCode();
        List<StockMovement> movements;
        MovementKind movementKind;
        if (kind == AdjustmentKind.AdjustmentOut)
        {
            movementKind = MovementKind.AdjustmentOut;
            if (OnHand - quantity < 0 || OnHand - quantity < Reserved)
            {
                throw new DomainException(InventoryErrors.AdjustmentBelowZero, "The adjustment would leave on hand below zero or below the reserved quantity.", "quantity");
            }

            movements = batchId is { } id
                ? [DecreaseBatch(MovementKind.AdjustmentOut, id, quantity, reasonCode, notes, referenceId, now, recordedBy)]
                : TakeFefo(MovementKind.AdjustmentOut, quantity, reasonCode, notes, MovementReferenceType.Manual, referenceId, now, recordedBy, out _);
        }
        else
        {
            movementKind = kind == AdjustmentKind.Return ? MovementKind.Return : MovementKind.AdjustmentIn;
            if (kind == AdjustmentKind.Return && batchId is null)
            {
                throw Invalid("batchId", "A RETURN must name the batch the stock goes back into.");
            }

            var target = IncreaseTarget(batchId, now);
            movements = [Post(movementKind, target, quantity, reasonCode, notes, MovementReferenceType.Manual, referenceId, now, recordedBy)];
        }

        Raise(new StockAdjusted(Id, FulfillmentLocationId, VariantId, movementKind.ToCode(), reasonCode, movements.Sum(m => m.Quantity), movements.Sum(m => m.CostPaise)));
        RaiseLevelChanges(before);
        return movements;
    }

    /// <summary>Physical count: COUNT_CORRECTION for counted − on hand (none when equal). May over-commit (D7).</summary>
    public IReadOnlyList<StockMovement> Count(int countedQuantity, Guid? batchId, string? notes, Guid referenceId, DateTimeOffset now, Guid? recordedBy)
    {
        if (countedQuantity < 0)
        {
            throw Invalid("countedQuantity.value", "Counted quantity must not be negative.");
        }

        var difference = countedQuantity - OnHand;
        if (difference == 0)
        {
            return [];
        }

        var before = Snapshot();
        List<StockMovement> movements;
        if (difference < 0)
        {
            movements = batchId is { } id
                ? [DecreaseBatch(MovementKind.CountCorrection, id, -difference, null, notes, referenceId, now, recordedBy)]
                : TakeFefo(MovementKind.CountCorrection, -difference, null, notes, MovementReferenceType.Manual, referenceId, now, recordedBy, out _);
        }
        else
        {
            var target = IncreaseTarget(batchId, now);
            movements = [Post(MovementKind.CountCorrection, target, difference, null, notes, MovementReferenceType.Manual, referenceId, now, recordedBy)];
        }

        Raise(new StockAdjusted(Id, FulfillmentLocationId, VariantId, MovementKind.CountCorrection.ToCode(), null, difference, movements.Sum(m => m.CostPaise)));
        RaiseLevelChanges(before);
        return movements;
    }

    /// <summary>Whether <paramref name="quantity"/> more can be reserved without over-committing (INV-I4).</summary>
    public bool CanReserve(int quantity) => quantity <= Available;

    /// <summary>Reserves gross stock for an order item (INV-R19). Refuses with OUT_OF_STOCK when not available.</summary>
    public StockReservation Reserve(Guid orderId, Guid orderItemId, int quantity, DateTimeOffset? expiresAt)
    {
        EnsurePositive(quantity, "quantity");
        if (!CanReserve(quantity))
        {
            throw new DomainException(InventoryErrors.OutOfStock, "Not enough stock is available for this item.", "quantity");
        }

        var before = Snapshot();
        var reservation = new StockReservation(Id, orderId, orderItemId, quantity, expiresAt);
        _reservations.Add(reservation);
        Reserved += quantity;
        RaiseLevelChanges(before);
        return reservation;
    }

    /// <summary>Order confirmed: its ACTIVE reservations no longer lapse. Returns how many changed.</summary>
    public int ClearReservationExpiry(Guid orderId)
    {
        var changed = 0;
        foreach (var reservation in _reservations.Where(r => r.IsActive && r.OrderId == orderId && r.ExpiresAt is not null))
        {
            reservation.ClearExpiry();
            changed++;
        }

        return changed;
    }

    /// <summary>Closes the order's ACTIVE reservations as RELEASED (cancel) or EXPIRED (payment failed).</summary>
    public int CloseReservationsOfOrder(Guid orderId, ReservationStatus status)
        => CloseReservations(_reservations.Where(r => r.IsActive && r.OrderId == orderId).ToList(), status);

    /// <summary>Releases the ACTIVE reservation of one order item (short supply, substitution accepted).</summary>
    public int ReleaseReservationOfItem(Guid orderItemId)
        => CloseReservations(_reservations.Where(r => r.IsActive && r.OrderItemId == orderItemId).ToList(), ReservationStatus.Released);

    /// <summary>
    /// Packing of one order line (INV-R20): the ACTIVE reservation is CONSUMED and SALE movements are written FEFO for
    /// <paramref name="packedGross"/>. When batches cannot cover it, a batchless COUNT_CORRECTION + SALE pair records the gap
    /// (INV-R14). Returns false when the line was already consumed (no ACTIVE reservation and a SALE exists).
    /// </summary>
    public bool ConsumeForPack(Guid orderItemId, int packedGross, bool saleAlreadyRecorded, DateTimeOffset now)
    {
        var reservation = _reservations.FirstOrDefault(r => r.IsActive && r.OrderItemId == orderItemId);
        if (reservation is null && saleAlreadyRecorded)
        {
            return false;
        }

        var before = Snapshot();
        if (reservation is not null)
        {
            reservation.Close(ReservationStatus.Consumed);
            Reserved -= reservation.Quantity;
        }

        if (packedGross > 0)
        {
            TakeFefo(MovementKind.Sale, packedGross, null, null, MovementReferenceType.OrderItem, orderItemId, now, null, out var gap);
            if (gap > 0)
            {
                Post(MovementKind.CountCorrection, null, gap, SystemReasonCodes.PackShortfall, null, MovementReferenceType.OrderItem, orderItemId, now, null);
                Post(MovementKind.Sale, null, -gap, SystemReasonCodes.PackShortfall, null, MovementReferenceType.OrderItem, orderItemId, now, null);
                Raise(new StockDiscrepancyFound(Id, FulfillmentLocationId, VariantId, orderItemId, gap));
            }
        }

        RaiseLevelChanges(before);
        return true;
    }

    /// <summary>Expiry job: every loaded ACTIVE batch past best-before is written off as WASTE EXPIRED and marked EXPIRED.</summary>
    public IReadOnlyList<Batch> ExpireDueBatches(DateTimeOffset now)
    {
        var due = _batches.Where(b => b.IsActive && b.BestBeforeAt <= now).OrderBy(b => b.BestBeforeAt).ThenBy(b => b.ReceivedAt).ToList();
        if (due.Count == 0)
        {
            return due;
        }

        var before = Snapshot();
        foreach (var batch in due)
        {
            var quantity = batch.RemainingQuantity;
            var cost = 0L;
            if (quantity > 0)
            {
                cost = Post(MovementKind.Waste, batch, -quantity, SystemReasonCodes.Expired, null, MovementReferenceType.Batch, batch.Id, now, null).CostPaise;
            }

            batch.MarkExpired();
            Raise(new BatchExpired(batch.Id, Id, FulfillmentLocationId, VariantId, quantity, cost));
        }

        RaiseLevelChanges(before);
        return due;
    }

    private int ActiveBatchQuantity => _batches.Where(b => b.IsActive).Sum(b => b.RemainingQuantity);

    private static void EnsureThreshold(int threshold)
    {
        if (threshold < 0)
        {
            throw Invalid("lowStockThreshold", "Low-stock threshold must not be negative.");
        }
    }

    private static void EnsurePositive(int quantity, string field)
    {
        if (quantity <= 0)
        {
            throw Invalid(field, "Quantity must be greater than zero.");
        }
    }

    private static RefusalException Invalid(string field, string message)
        => new(new FieldError(field, InventoryErrors.ValidationFailed, message));

    private int CloseReservations(List<StockReservation> reservations, ReservationStatus status)
    {
        if (reservations.Count == 0)
        {
            return 0;
        }

        var before = Snapshot();
        foreach (var reservation in reservations)
        {
            reservation.Close(status);
            Reserved -= reservation.Quantity;
        }

        RaiseLevelChanges(before);
        return reservations.Count;
    }

    private Batch RequireBatch(Guid batchId, bool forIncrease, DateTimeOffset now)
    {
        var batch = _batches.FirstOrDefault(b => b.Id == batchId)
            ?? throw Invalid("batchId", "The batch does not belong to this stock item.");
        var usable = batch.Status switch
        {
            BatchStatus.Active => true,
            BatchStatus.Depleted => forIncrease && batch.BestBeforeAt > now,
            _ => false,
        };
        return usable
            ? batch
            : throw new DomainException(InventoryErrors.BatchNotActive, "The batch is depleted, expired or discarded.", "batchId");
    }

    private Batch IncreaseTarget(Guid? batchId, DateTimeOffset now)
    {
        if (batchId is { } id)
        {
            return RequireBatch(id, forIncrease: true, now);
        }

        return _batches.Where(b => b.IsActive).OrderByDescending(b => b.ReceivedAt).ThenByDescending(b => b.Id).FirstOrDefault()
            ?? throw Invalid("batchId", "There is no active batch; name the batch to increase.");
    }

    private StockMovement DecreaseBatch(MovementKind kind, Guid batchId, int quantity, string? reasonCode, string? notes, Guid referenceId, DateTimeOffset now, Guid? recordedBy)
    {
        var batch = RequireBatch(batchId, forIncrease: false, now);
        if (batch.RemainingQuantity < quantity)
        {
            throw Invalid("quantity", "Quantity is more than the batch holds.");
        }

        return Post(kind, batch, -quantity, reasonCode, notes, MovementReferenceType.Manual, referenceId, now, recordedBy);
    }

    /// <summary>FEFO: best-before, then received, then id (INV-R14). Returns the movements; <paramref name="gap"/> is what the batches could not cover.</summary>
    private List<StockMovement> TakeFefo(
        MovementKind kind,
        int need,
        string? reasonCode,
        string? notes,
        MovementReferenceType referenceType,
        Guid referenceId,
        DateTimeOffset now,
        Guid? recordedBy,
        out int gap)
    {
        var movements = new List<StockMovement>();
        var remaining = need;
        foreach (var batch in _batches.Where(b => b.IsActive && b.RemainingQuantity > 0).OrderBy(b => b.BestBeforeAt).ThenBy(b => b.ReceivedAt).ThenBy(b => b.Id).ToList())
        {
            if (remaining == 0)
            {
                break;
            }

            var take = Math.Min(batch.RemainingQuantity, remaining);
            movements.Add(Post(kind, batch, -take, reasonCode, notes, referenceType, referenceId, now, recordedBy));
            remaining -= take;
        }

        gap = remaining;
        return movements;
    }

    private StockMovement Post(
        MovementKind kind,
        Batch? batch,
        int quantity,
        string? reasonCode,
        string? notes,
        MovementReferenceType referenceType,
        Guid? referenceId,
        DateTimeOffset now,
        Guid? recordedBy)
    {
        var cost = batch?.Apply(quantity) ?? 0;
        OnHand += quantity;
        var movement = new StockMovement(Id, batch?.Id, kind, quantity, cost, reasonCode, notes, referenceType, referenceId, now, recordedBy);
        _movements.Add(movement);
        return movement;
    }

    private void RaiseWaste(string reasonCode, List<StockMovement> movements)
        => Raise(new WasteRecorded(
            Id,
            FulfillmentLocationId,
            VariantId,
            reasonCode,
            [.. movements.Select(m => new WasteLine(m.BatchId!.Value, m.Quantity, m.CostPaise))],
            movements.Sum(m => m.Quantity),
            movements.Sum(m => m.CostPaise)));

    private Level Snapshot() => new(Available, LowStockThreshold, Math.Max(0, Reserved - OnHand));

    /// <summary>INV-R17 edges and the over-commit signal (D7), compared with the level before the change.</summary>
    private void RaiseLevelChanges(Level before)
    {
        var after = Snapshot();
        if (after.Threshold > 0 && before.Available > before.Threshold && after.Available <= after.Threshold)
        {
            Raise(new LowStockReached(Id, FulfillmentLocationId, VariantId, after.Available, after.Threshold));
        }

        if (before.Available > 0 && after.Available <= 0)
        {
            Raise(new OutOfStockReached(Id, FulfillmentLocationId, VariantId, after.Available, after.Threshold));
        }

        if (before.Available <= before.Threshold && after.Available > after.Threshold)
        {
            Raise(new StockReplenished(Id, FulfillmentLocationId, VariantId, after.Available, after.Threshold));
        }

        if (after.Shortfall > before.Shortfall)
        {
            Raise(new StockOverCommitted(Id, FulfillmentLocationId, VariantId, OnHand, Reserved, after.Shortfall));
        }
    }

    private readonly record struct Level(int Available, int Threshold, int Shortfall);
}
