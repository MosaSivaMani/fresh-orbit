namespace FreshOrbit.Inventory.Domain.Stock;

/// <summary>Loads and tracks <see cref="StockItem"/>s on the shared unit of work. Every loader is one query.</summary>
public interface IStockItemRepository
{
    /// <summary>Root + ACTIVE batches + ACTIVE reservations (plus the batch <paramref name="includeBatchId"/> whatever its status).</summary>
    Task<StockItem?> GetWithActiveChildrenAsync(Guid stockItemId, Guid? includeBatchId = null, CancellationToken cancellationToken = default);

    /// <summary>The stock item owning a batch, with active children and that batch.</summary>
    Task<StockItem?> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<StockItem?> GetByLocationAndVariantAsync(Guid fulfillmentLocationId, Guid variantId, bool withActiveChildren, CancellationToken cancellationToken = default);

    /// <summary>Roots only (for reservation), one query.</summary>
    Task<IReadOnlyList<StockItem>> GetForReservationAsync(Guid fulfillmentLocationId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default);

    /// <summary>Stock items holding reservations of the order (any status), with active children.</summary>
    Task<IReadOnlyList<StockItem>> GetForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Stock items holding ACTIVE reservations of the given order items, with active children.</summary>
    Task<IReadOnlyList<StockItem>> GetForOrderItemsAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken = default);

    /// <summary>Stock items of (location × variants), with active children.</summary>
    Task<IReadOnlyList<StockItem>> GetManyWithActiveChildrenAsync(Guid fulfillmentLocationId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default);

    /// <summary>Order items that already have a SALE movement (pack idempotency guard).</summary>
    Task<IReadOnlySet<Guid>> GetOrderItemsWithSaleAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken = default);

    Task<bool> BatchCodeExistsAsync(Guid fulfillmentLocationId, string batchCode, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid fulfillmentLocationId, Guid variantId, CancellationToken cancellationToken = default);

    /// <summary>Ids of stock items with an ACTIVE batch whose best-before has passed.</summary>
    Task<IReadOnlyList<Guid>> GetIdsWithExpiredBatchesAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    void Add(StockItem stockItem);

    /// <summary>Marks the root modified so its row version moves even when only children changed.</summary>
    void MarkChanged(StockItem stockItem);
}
