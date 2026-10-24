using FreshOrbit.Catalog.Public;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Ordering.Infrastructure.Public;

/// <summary>
/// Catalog's bestseller ranking (CAT-R24) from real orders: products by the number of distinct sold orders at the
/// location placed since the window start. Sold = CONFIRMED through DELIVERED; a line counts while ACTIVE or
/// SHORT_SUPPLIED (cancelled and substituted-away lines do not). Ties: most recent order first, then product id.
/// One query; read-only.
/// </summary>
internal sealed class OrderingBestsellerSource(FreshOrbitDbContext db) : IBestsellerSource
{
    private static readonly OrderStatus[] SoldStatuses = [OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Packed, OrderStatus.OutForDelivery, OrderStatus.Delivered];
    private static readonly OrderItemStatus[] CountedLines = [OrderItemStatus.Active, OrderItemStatus.ShortSupplied];

    public async Task<IReadOnlyList<Guid>> GetBestsellingProductIdsAsync(Guid fulfillmentLocationId, DateTimeOffset since, int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return [];
        }

        var rows = await db.Set<Order>().AsNoTracking()
            .Where(o => o.FulfillmentLocationId == fulfillmentLocationId && o.PlacedAt >= since && SoldStatuses.Contains(o.Status))
            .SelectMany(o => o.Items.Where(i => CountedLines.Contains(i.Status)).Select(i => new { i.ProductId, OrderId = o.Id, o.PlacedAt }))
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Orders = g.Select(x => x.OrderId).Distinct().Count(), LastPlacedAt = g.Max(x => x.PlacedAt) })
            .OrderByDescending(x => x.Orders)
            .ThenByDescending(x => x.LastPlacedAt)
            .ThenBy(x => x.ProductId)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => r.ProductId)];
    }
}
