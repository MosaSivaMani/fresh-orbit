using FreshOrbit.Catalog.Public;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Inventory.Infrastructure.Public;

/// <summary>
/// Catalog's freshness facts (CAT-R25): per variant, the batch the next order is packed from — FEFO, the same order as
/// picking (ACTIVE, quantity left, best-before after <c>at</c>; earliest best-before, then earliest received). Its
/// recorded catch/harvest date and receipt time. One query; read-only.
/// </summary>
internal sealed class InventoryFreshnessSource(FreshOrbitDbContext db) : IVariantFreshnessSource
{
    public async Task<IReadOnlyList<VariantFreshness>> GetFreshnessAsync(Guid fulfillmentLocationId, IReadOnlyCollection<Guid> variantIds, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(variantIds);
        if (variantIds.Count == 0)
        {
            return [];
        }

        var batches = await db.Set<Batch>().AsNoTracking()
            .Where(b => b.FulfillmentLocationId == fulfillmentLocationId
                && variantIds.Contains(b.VariantId)
                && b.Status == BatchStatus.Active
                && b.RemainingQuantity > 0
                && b.BestBeforeAt > at)
            .Select(b => new { b.Id, b.VariantId, b.CaughtOrHarvestedOn, b.ReceivedAt, b.BestBeforeAt })
            .ToListAsync(cancellationToken);
        return [.. batches
            .GroupBy(b => b.VariantId)
            .Select(g => g.OrderBy(b => b.BestBeforeAt).ThenBy(b => b.ReceivedAt).ThenBy(b => b.Id).First())
            .Select(b => new VariantFreshness(b.VariantId, b.CaughtOrHarvestedOn, b.ReceivedAt))];
    }
}
