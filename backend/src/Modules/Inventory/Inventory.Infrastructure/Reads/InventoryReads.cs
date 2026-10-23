using FreshOrbit.Inventory.Application.Common;
using FreshOrbit.Inventory.Application.Stock;
using FreshOrbit.Inventory.Domain.ColdChain;
using FreshOrbit.Inventory.Domain.Shared;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Paging;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Inventory.Infrastructure.Reads;

public sealed record StockListQuery(Guid? FulfillmentLocationId, Guid? VariantId, bool LowStockOnly);

public sealed record BatchListQuery(Guid? FulfillmentLocationId, Guid? VariantId, Guid? StockItemId, IReadOnlyList<BatchStatus> Statuses, DateTimeOffset? ExpiringBefore);

public sealed record MovementListQuery(Guid? StockItemId, Guid? FulfillmentLocationId, IReadOnlyList<MovementKind> Kinds, DateOnly? FromDate, DateOnly? ToDate);

public sealed record ReservationListQuery(Guid? FulfillmentLocationId, Guid? OrderId, IReadOnlyList<ReservationStatus> Statuses);

public sealed record ColdChainListQuery(Guid? FulfillmentLocationId, bool OutOfRangeOnly);

/// <summary>
/// Admin reads (no Decide/Commit), all location-scoped: staff limited to some locations see only those rows
/// (silent filter, INV-R10); a single resource at another location is 403.
/// </summary>
public sealed class InventoryReads(FreshOrbitDbContext db, ICurrentActor actor)
{
    public async Task<PagedResponse<StockItemResponse>> ListStockAsync(StockListQuery query, PagedRequest paging, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var items = ScopedStockItems();
        if (query.FulfillmentLocationId is { } locationId)
        {
            items = items.Where(s => s.FulfillmentLocationId == locationId);
        }

        if (query.VariantId is { } variantId)
        {
            items = items.Where(s => s.VariantId == variantId);
        }

        if (query.LowStockOnly)
        {
            items = items.Where(s => s.LowStockThreshold > 0 && s.OnHand - s.Reserved <= s.LowStockThreshold);
        }

        var total = await items.CountAsync(cancellationToken);
        var page = await items.OrderBy(s => s.FulfillmentLocationId).ThenBy(s => s.Id).Skip(Skip(paging)).Take(paging.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<StockItemResponse>([.. page.Select(s => s.ToResponse(includeBatches: false))], paging.Page, paging.PageSize, total);
    }

    public async Task<StockItemResponse> GetStockAsync(Guid stockItemId, CancellationToken cancellationToken)
    {
        var item = await db.Set<StockItem>().AsNoTracking()
            .Include(s => s.Batches.Where(b => b.Status == BatchStatus.Active))
            .FirstOrDefaultAsync(s => s.Id == stockItemId, cancellationToken)
            ?? throw new NotFoundException("No stock item has this id.");
        LocationScope.Ensure(actor, item.FulfillmentLocationId);
        return item.ToResponse(includeBatches: true);
    }

    public async Task<PagedResponse<BatchResponse>> ListBatchesAsync(BatchListQuery query, PagedRequest paging, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var rows = db.Set<Batch>().AsNoTracking()
            .Join(ScopedStockItems(), b => b.StockItemId, s => s.Id, (b, s) => new { Batch = b, s.StockUnit, s.Version });
        if (query.FulfillmentLocationId is { } locationId)
        {
            rows = rows.Where(r => r.Batch.FulfillmentLocationId == locationId);
        }

        if (query.VariantId is { } variantId)
        {
            rows = rows.Where(r => r.Batch.VariantId == variantId);
        }

        if (query.StockItemId is { } stockItemId)
        {
            rows = rows.Where(r => r.Batch.StockItemId == stockItemId);
        }

        if (query.Statuses.Count > 0)
        {
            var statuses = query.Statuses.ToList();
            rows = rows.Where(r => statuses.Contains(r.Batch.Status));
        }

        if (query.ExpiringBefore is { } before)
        {
            rows = rows.Where(r => r.Batch.BestBeforeAt < before);
        }

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows.OrderBy(r => r.Batch.BestBeforeAt).ThenBy(r => r.Batch.Id).Skip(Skip(paging)).Take(paging.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<BatchResponse>([.. page.Select(r => r.Batch.ToResponse(r.StockUnit, StockMappings.FormatVersion(r.Version)))], paging.Page, paging.PageSize, total);
    }

    public async Task<PagedResponse<MovementResponse>> ListMovementsAsync(MovementListQuery query, PagedRequest paging, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var rows = db.Set<StockMovement>().AsNoTracking()
            .Join(ScopedStockItems(), m => m.StockItemId, s => s.Id, (m, s) => new { Movement = m, s.StockUnit, s.FulfillmentLocationId });
        if (query.StockItemId is { } stockItemId)
        {
            rows = rows.Where(r => r.Movement.StockItemId == stockItemId);
        }

        if (query.FulfillmentLocationId is { } locationId)
        {
            rows = rows.Where(r => r.FulfillmentLocationId == locationId);
        }

        if (query.Kinds.Count > 0)
        {
            var kinds = query.Kinds.ToList();
            rows = rows.Where(r => kinds.Contains(r.Movement.Kind));
        }

        if (query.FromDate is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            rows = rows.Where(r => r.Movement.OccurredAt >= start);
        }

        if (query.ToDate is { } to)
        {
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            rows = rows.Where(r => r.Movement.OccurredAt < end);
        }

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows.OrderByDescending(r => r.Movement.OccurredAt).ThenByDescending(r => r.Movement.Id).Skip(Skip(paging)).Take(paging.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<MovementResponse>([.. page.Select(r => r.Movement.ToResponse(r.StockUnit))], paging.Page, paging.PageSize, total);
    }

    public async Task<PagedResponse<ReservationResponse>> ListReservationsAsync(ReservationListQuery query, PagedRequest paging, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var rows = db.Set<StockReservation>().AsNoTracking()
            .Join(ScopedStockItems(), r => r.StockItemId, s => s.Id, (r, s) => new
            {
                Reservation = r,
                s.StockUnit,
                s.FulfillmentLocationId,
                s.VariantId,
                CreatedAt = EF.Property<DateTimeOffset>(r, "CreatedAt"),
            });
        var statuses = query.Statuses.Count > 0 ? query.Statuses.ToList() : [ReservationStatus.Active];
        rows = rows.Where(r => statuses.Contains(r.Reservation.Status));
        if (query.FulfillmentLocationId is { } locationId)
        {
            rows = rows.Where(r => r.FulfillmentLocationId == locationId);
        }

        if (query.OrderId is { } orderId)
        {
            rows = rows.Where(r => r.Reservation.OrderId == orderId);
        }

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows.OrderBy(r => r.CreatedAt).ThenBy(r => r.Reservation.Id).Skip(Skip(paging)).Take(paging.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<ReservationResponse>(
            [.. page.Select(r => new ReservationResponse(
                r.Reservation.Id,
                r.Reservation.StockItemId,
                r.FulfillmentLocationId,
                r.VariantId,
                r.Reservation.OrderId,
                r.Reservation.OrderItemId,
                StockMappings.Quantity(r.Reservation.Quantity, r.StockUnit),
                r.Reservation.Status,
                r.Reservation.ExpiresAt,
                r.CreatedAt))],
            paging.Page,
            paging.PageSize,
            total);
    }

    public async Task<PagedResponse<ColdChainLogResponse>> ListColdChainAsync(ColdChainListQuery query, PagedRequest paging, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var logs = db.Set<ColdChainLog>().AsNoTracking();
        if (!actor.IsAllLocations)
        {
            var allowed = actor.LocationIds.ToList();
            logs = logs.Where(l => allowed.Contains(l.FulfillmentLocationId));
        }

        if (query.FulfillmentLocationId is { } locationId)
        {
            logs = logs.Where(l => l.FulfillmentLocationId == locationId);
        }

        if (query.OutOfRangeOnly)
        {
            logs = logs.Where(l => l.IsOutOfRange);
        }

        var total = await logs.CountAsync(cancellationToken);
        var page = await logs.OrderByDescending(l => l.RecordedAt).ThenByDescending(l => l.Id).Skip(Skip(paging)).Take(paging.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<ColdChainLogResponse>([.. page.Select(l => l.ToResponse())], paging.Page, paging.PageSize, total);
    }

    private static int Skip(PagedRequest paging)
    {
        if (paging.Page < 1)
        {
            throw new RefusalException(new FieldError("page", InventoryErrors.ValidationFailed, "Page must be 1 or more."));
        }

        if (paging.PageSize is < 1 or > PagedRequest.MaxPageSize)
        {
            throw new RefusalException(new FieldError("pageSize", InventoryErrors.ValidationFailed, "Page size must be between 1 and 100."));
        }

        return paging.Skip;
    }

    private IQueryable<StockItem> ScopedStockItems()
    {
        var items = db.Set<StockItem>().AsNoTracking();
        if (actor.IsAllLocations)
        {
            return items;
        }

        var allowed = actor.LocationIds.ToList();
        return items.Where(s => allowed.Contains(s.FulfillmentLocationId));
    }
}
