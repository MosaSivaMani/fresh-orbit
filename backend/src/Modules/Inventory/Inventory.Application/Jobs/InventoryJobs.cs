using FreshOrbit.Inventory.Application.Common;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Domain.Stock.Events;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Inventory.Application.Jobs;

/// <summary>A stock item at or below its threshold (low-stock digest input).</summary>
public sealed record LowStockRow(Guid FulfillmentLocationId, Guid StockItemId, Guid VariantId, int Available, int Threshold);

/// <summary>Queries of the recurring jobs (each one query).</summary>
public interface IInventoryJobQueries
{
    /// <summary>Items with threshold &gt; 0 and on hand − reserved ≤ threshold.</summary>
    Task<IReadOnlyList<LowStockRow>> GetLowStockAsync(CancellationToken cancellationToken = default);

    /// <summary>Locations (of <paramref name="fulfillmentLocationIds"/>) that already have a digest for their local date.</summary>
    Task<IReadOnlySet<(Guid FulfillmentLocationId, DateOnly LocalDate)>> GetSentDigestsAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, DateOnly fromDate, CancellationToken cancellationToken = default);

    void Add(LowStockDigest digest);
}

/// <summary>
/// Batch expiry (every 15 min): ACTIVE batches past best-before are written off as WASTE EXPIRED, one transaction per
/// stock item. No transaction is opened when nothing is due; a lost race skips the item until the next run.
/// </summary>
public sealed class BatchExpiryService(IStockItemRepository stockItems, IInventoryUnitOfWork unitOfWork, IClock clock)
{
    /// <summary>Returns the number of stock items changed.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var ids = await stockItems.GetIdsWithExpiredBatchesAsync(now, cancellationToken);
        var changed = 0;
        foreach (var id in ids)
        {
            try
            {
                var item = await stockItems.GetWithActiveChildrenAsync(id, null, cancellationToken);
                if (item is null || item.ExpireDueBatches(now).Count == 0)
                {
                    continue;
                }

                stockItems.MarkChanged(item);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                changed++;
            }
            catch (ConcurrentSaveException)
            {
                // Another command changed the item; the next run retries it.
            }
            finally
            {
                unitOfWork.ClearChanges();
            }
        }

        return changed;
    }
}

/// <summary>
/// Low-stock digest (hourly): one <c>LowStockDigestReady</c> per location whose local time is 07:00–07:59 and that has
/// items at or below threshold, at most once per local date.
/// </summary>
public sealed class LowStockDigestService(IInventoryJobQueries queries, ILocationsApi locations, IInventoryUnitOfWork unitOfWork, IClock clock)
{
    public const int DigestHour = 7;

    /// <summary>Returns the number of digests raised.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var rows = await queries.GetLowStockAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        var now = clock.UtcNow;
        var byLocation = rows.GroupBy(r => r.FulfillmentLocationId).ToDictionary(g => g.Key, g => g.ToList());
        var due = new List<(Guid LocationId, DateOnly LocalDate)>();
        foreach (var location in await locations.GetFulfillmentLocationsAsync([.. byLocation.Keys], cancellationToken))
        {
            var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone));
            if (local.Hour == DigestHour)
            {
                due.Add((location.Id, DateOnly.FromDateTime(local.DateTime)));
            }
        }

        if (due.Count == 0)
        {
            return 0;
        }

        var sent = await queries.GetSentDigestsAsync([.. due.Select(d => d.LocationId)], due.Min(d => d.LocalDate), cancellationToken);
        var raised = 0;
        foreach (var (locationId, localDate) in due.Where(d => !sent.Contains(d)))
        {
            var items = byLocation[locationId].Select(r => new LowStockDigestItem(r.StockItemId, r.VariantId, r.Available, r.Threshold)).ToList();
            queries.Add(LowStockDigest.Create(locationId, localDate, items));
            raised++;
        }

        if (raised > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return raised;
    }
}
