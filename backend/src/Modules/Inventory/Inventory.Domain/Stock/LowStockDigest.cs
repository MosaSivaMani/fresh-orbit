using FreshOrbit.Inventory.Domain.Stock.Events;
using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Inventory.Domain.Stock;

/// <summary>
/// Record of the daily low-stock digest of one location (unique per location and local date), so the hourly job
/// sends at most one digest a day even when it runs more than once in the 07:00 window.
/// </summary>
public sealed class LowStockDigest : AggregateRoot
{
    private LowStockDigest()
    {
    }

    private LowStockDigest(Guid fulfillmentLocationId, DateOnly localDate, int itemCount)
    {
        FulfillmentLocationId = fulfillmentLocationId;
        LocalDate = localDate;
        ItemCount = itemCount;
    }

    public Guid FulfillmentLocationId { get; private set; }

    public DateOnly LocalDate { get; private set; }

    public int ItemCount { get; private set; }

    public static LowStockDigest Create(Guid fulfillmentLocationId, DateOnly localDate, IReadOnlyList<LowStockDigestItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var digest = new LowStockDigest(fulfillmentLocationId, localDate, items.Count);
        digest.Raise(new LowStockDigestReady(fulfillmentLocationId, items));
        return digest;
    }
}
