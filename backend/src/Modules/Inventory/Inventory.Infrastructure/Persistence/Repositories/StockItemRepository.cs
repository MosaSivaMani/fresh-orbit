using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Inventory.Infrastructure.Persistence.Repositories;

internal sealed class StockItemRepository(FreshOrbitDbContext db) : IStockItemRepository
{
    public Task<StockItem?> GetWithActiveChildrenAsync(Guid stockItemId, Guid? includeBatchId = null, CancellationToken cancellationToken = default)
        => WithActiveChildren(includeBatchId).FirstOrDefaultAsync(s => s.Id == stockItemId, cancellationToken);

    public Task<StockItem?> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
        => WithActiveChildren(batchId).FirstOrDefaultAsync(s => db.Set<Batch>().Any(b => b.Id == batchId && b.StockItemId == s.Id), cancellationToken);

    public Task<StockItem?> GetByLocationAndVariantAsync(Guid fulfillmentLocationId, Guid variantId, bool withActiveChildren, CancellationToken cancellationToken = default)
        => (withActiveChildren ? WithActiveChildren(null) : db.Set<StockItem>())
            .FirstOrDefaultAsync(s => s.FulfillmentLocationId == fulfillmentLocationId && s.VariantId == variantId, cancellationToken);

    public async Task<IReadOnlyList<StockItem>> GetForReservationAsync(Guid fulfillmentLocationId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default)
        => await db.Set<StockItem>()
            .Where(s => s.FulfillmentLocationId == fulfillmentLocationId && variantIds.Contains(s.VariantId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StockItem>> GetForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        => await WithActiveChildren(null)
            .Where(s => db.Set<StockReservation>().Any(r => r.StockItemId == s.Id && r.OrderId == orderId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StockItem>> GetForOrderItemsAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken = default)
        => await WithActiveChildren(null)
            .Where(s => db.Set<StockReservation>().Any(r => r.StockItemId == s.Id && r.Status == ReservationStatus.Active && orderItemIds.Contains(r.OrderItemId)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StockItem>> GetManyWithActiveChildrenAsync(Guid fulfillmentLocationId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default)
        => await WithActiveChildren(null)
            .Where(s => s.FulfillmentLocationId == fulfillmentLocationId && variantIds.Contains(s.VariantId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<Guid>> GetOrderItemsWithSaleAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken = default)
        => (await db.Set<StockMovement>()
                .Where(m => m.Kind == MovementKind.Sale && m.ReferenceType == MovementReferenceType.OrderItem && m.ReferenceId != null && orderItemIds.Contains(m.ReferenceId.Value))
                .Select(m => m.ReferenceId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

    public Task<bool> BatchCodeExistsAsync(Guid fulfillmentLocationId, string batchCode, CancellationToken cancellationToken = default)
    {
        var code = batchCode.Trim();
        return db.Set<Batch>().AnyAsync(b => b.FulfillmentLocationId == fulfillmentLocationId && b.BatchCode == code, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid fulfillmentLocationId, Guid variantId, CancellationToken cancellationToken = default)
        => db.Set<StockItem>().AnyAsync(s => s.FulfillmentLocationId == fulfillmentLocationId && s.VariantId == variantId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetIdsWithExpiredBatchesAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        => await db.Set<Batch>()
            .Where(b => b.Status == BatchStatus.Active && b.BestBeforeAt <= now)
            .Select(b => b.StockItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public void Add(StockItem stockItem) => db.Add(stockItem);

    public void MarkChanged(StockItem stockItem)
    {
        var entry = db.Entry(stockItem);
        if (entry.State is EntityState.Unchanged or EntityState.Modified)
        {
            entry.Property(s => s.LowStockThreshold).IsModified = true;
        }
    }

    private IQueryable<StockItem> WithActiveChildren(Guid? includeBatchId)
        => db.Set<StockItem>()
            .Include(s => s.Batches.Where(b => b.Status == BatchStatus.Active || b.Id == includeBatchId))
            .Include(s => s.Reservations.Where(r => r.Status == ReservationStatus.Active))
            .AsSplitQuery();
}
