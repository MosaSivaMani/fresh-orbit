using FreshOrbit.Inventory.Domain.Shared;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Public;

namespace FreshOrbit.Inventory.Application.Stock;

/// <summary>
/// Checkout reservation and substitution release (INV-R19, R24). Only tracks changes on the shared unit of work; the
/// caller (Ordering) saves once. Refusals change nothing, so a retry from Read is safe.
/// </summary>
public sealed class StockReservationService(IStockItemRepository stockItems)
{
    public async Task<ReserveStockResult> ReserveAsync(ReserveStockRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Lines.Any(l => l.Quantity <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Every reservation line needs a quantity greater than zero.");
        }

        var variantIds = request.Lines.Select(l => l.VariantId).Distinct().ToList();
        var items = (await stockItems.GetForReservationAsync(request.FulfillmentLocationId, variantIds, cancellationToken)).ToDictionary(i => i.VariantId);

        // Decide for all lines before changing anything (all or nothing; two lines of one variant share its stock).
        var refusals = new List<ReserveStockRefusal>();
        foreach (var group in request.Lines.GroupBy(l => l.VariantId))
        {
            if (!items.TryGetValue(group.Key, out var item))
            {
                refusals.AddRange(group.Select(l => new ReserveStockRefusal(l.OrderItemId, l.VariantId, InventoryErrors.StockItemMissing, l.Quantity, 0)));
                continue;
            }

            var needed = group.Sum(l => l.Quantity);
            if (!item.CanReserve(needed))
            {
                refusals.AddRange(group.Select(l => new ReserveStockRefusal(l.OrderItemId, l.VariantId, InventoryErrors.OutOfStock, l.Quantity, Math.Max(0, item.Available))));
            }
        }

        if (refusals.Count > 0)
        {
            return new ReserveStockResult(false, [.. refusals.Select(r => r.VariantId).Distinct()]) { Refusals = refusals };
        }

        foreach (var line in request.Lines)
        {
            items[line.VariantId].Reserve(request.OrderId, line.OrderItemId, line.Quantity, request.ExpiresAt);
        }

        return new ReserveStockResult(true, []);
    }

    public async Task ReleaseAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderItemIds);
        if (orderItemIds.Count == 0)
        {
            return;
        }

        foreach (var item in await stockItems.GetForOrderItemsAsync(orderItemIds, cancellationToken))
        {
            var released = orderItemIds.Sum(item.ReleaseReservationOfItem);
            if (released > 0)
            {
                stockItems.MarkChanged(item);
            }
        }
    }
}
