using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Inventory.Domain.Stock;

/// <summary>
/// One received lot of a stock item. Changed only through its <see cref="StockItem"/>.
/// <see cref="RemainingCostPaise"/> is the sum of the cost of every movement of the batch (RECEIPT included), so the
/// movement that drains the batch can take exactly the remainder (INV-R15, D2).
/// </summary>
public sealed class Batch : Entity
{
    private Batch()
    {
        BatchCode = string.Empty;
    }

    internal Batch(
        StockItem stockItem,
        string batchCode,
        string? supplierName,
        string? sourceLandingCenter,
        DateOnly? caughtOrHarvestedOn,
        DateTimeOffset receivedAt,
        int receivedQuantity,
        DateTimeOffset bestBeforeAt,
        decimal? receiptTemperatureC,
        long totalCostPaise)
    {
        StockItemId = stockItem.Id;
        FulfillmentLocationId = stockItem.FulfillmentLocationId;
        VariantId = stockItem.VariantId;
        BatchCode = batchCode;
        SupplierName = supplierName;
        SourceLandingCenter = sourceLandingCenter;
        CaughtOrHarvestedOn = caughtOrHarvestedOn;
        ReceivedAt = receivedAt;
        ReceivedQuantity = receivedQuantity;
        RemainingQuantity = receivedQuantity;
        BestBeforeAt = bestBeforeAt;
        ReceiptTemperatureC = receiptTemperatureC;
        TotalCostPaise = totalCostPaise;
        RemainingCostPaise = totalCostPaise;
        Status = BatchStatus.Active;
    }

    public Guid StockItemId { get; private set; }

    public Guid FulfillmentLocationId { get; private set; }

    public Guid VariantId { get; private set; }

    public string BatchCode { get; private set; }

    public string? SupplierName { get; private set; }

    public string? SourceLandingCenter { get; private set; }

    public DateOnly? CaughtOrHarvestedOn { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public int ReceivedQuantity { get; private set; }

    public int RemainingQuantity { get; private set; }

    public DateTimeOffset BestBeforeAt { get; private set; }

    public decimal? ReceiptTemperatureC { get; private set; }

    public long TotalCostPaise { get; private set; }

    /// <summary>Book value of the remaining quantity: Σ cost_paise of the batch's movements.</summary>
    public long RemainingCostPaise { get; private set; }

    public BatchStatus Status { get; private set; }

    public bool IsActive => Status == BatchStatus.Active;

    /// <summary>
    /// Cost of a movement of <paramref name="quantity"/> (signed) from this batch: proportional to the batch total cost,
    /// rounded half away from zero; a movement that leaves the batch empty takes the exact remainder.
    /// </summary>
    public long CostOf(int quantity)
    {
        if (quantity < 0 && RemainingQuantity + quantity == 0)
        {
            return -RemainingCostPaise;
        }

        return ReceivedQuantity == 0 ? 0 : Rounding.DivideHalfAwayFromZero((Int128)quantity * TotalCostPaise, ReceivedQuantity);
    }

    /// <summary>Applies a signed movement; returns its cost. A batch reaching zero becomes DEPLETED, a DEPLETED one that grows ACTIVE.</summary>
    internal long Apply(int quantity)
    {
        var cost = CostOf(quantity);
        RemainingQuantity += quantity;
        RemainingCostPaise += cost;
        if (RemainingQuantity == 0 && Status == BatchStatus.Active)
        {
            Status = BatchStatus.Depleted;
        }
        else if (RemainingQuantity > 0 && Status == BatchStatus.Depleted)
        {
            Status = BatchStatus.Active;
        }

        return cost;
    }

    internal void MarkExpired() => Status = BatchStatus.Expired;

    internal void MarkDiscarded() => Status = BatchStatus.Discarded;
}
