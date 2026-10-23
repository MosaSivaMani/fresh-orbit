using FreshOrbit.Inventory.Application.Stock;
using FreshOrbit.Inventory.Domain.Shared;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Inventory.Infrastructure.Public;

/// <summary>Implementation of <see cref="IInventoryApi"/> on the shared DbContext.</summary>
internal sealed class InventoryApi(FreshOrbitDbContext db, StockReservationService reservations) : IInventoryApi
{
    public async Task<IReadOnlyList<VariantAvailability>> GetAvailabilityAsync(Guid fulfillmentLocationId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(variantIds);
        var rows = await db.Set<StockItem>().AsNoTracking()
            .Where(s => s.FulfillmentLocationId == fulfillmentLocationId && variantIds.Contains(s.VariantId))
            .Select(s => new { s.VariantId, s.OnHand, s.Reserved, s.StockUnit, s.LowStockThreshold })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new VariantAvailability(r.VariantId, Math.Max(0, r.OnHand - r.Reserved), ToUnit(r.StockUnit)) { LowStockThreshold = r.LowStockThreshold })];
    }

    public Task<ReserveStockResult> ReserveAsync(ReserveStockRequest request, CancellationToken cancellationToken = default)
        => reservations.ReserveAsync(request, cancellationToken);

    public Task ReleaseAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken = default)
        => reservations.ReleaseAsync(orderItemIds, cancellationToken);

    public async Task<OrderStock> GetOrderStockAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var reserved = await db.Set<StockReservation>().AsNoTracking()
            .Where(r => r.OrderId == orderId)
            .Join(db.Set<StockItem>(), r => r.StockItemId, s => s.Id, (r, s) => new { Reservation = r, s.VariantId, s.StockUnit })
            .OrderBy(r => r.Reservation.Id)
            .ToListAsync(cancellationToken);
        var orderItemIds = reserved.Select(r => r.Reservation.OrderItemId).Distinct().ToList();
        var sales = await db.Set<StockMovement>().AsNoTracking()
            .Where(m => m.Kind == MovementKind.Sale && m.ReferenceType == MovementReferenceType.OrderItem && m.ReferenceId != null && orderItemIds.Contains(m.ReferenceId.Value))
            .Join(db.Set<StockItem>(), m => m.StockItemId, s => s.Id, (m, s) => new { Movement = m, s.VariantId })
            .GroupJoin(db.Set<Batch>(), x => x.Movement.BatchId, b => b.Id, (x, batches) => new { x.Movement, x.VariantId, batches })
            .SelectMany(x => x.batches.DefaultIfEmpty(), (x, b) => new { x.Movement, x.VariantId, BatchCode = b == null ? null : b.BatchCode })
            .OrderBy(x => x.Movement.OccurredAt).ThenBy(x => x.Movement.Id)
            .ToListAsync(cancellationToken);
        return new OrderStock(
            orderId,
            [.. reserved.Select(r => new OrderStockReservation(r.Reservation.Id, r.Reservation.OrderItemId, r.VariantId, r.Reservation.Quantity, ToUnit(r.StockUnit), r.Reservation.Status.ToCode(), r.Reservation.ExpiresAt))],
            [.. sales.Select(s => new OrderStockSale(s.Movement.Id, s.Movement.ReferenceId!.Value, s.VariantId, s.Movement.BatchId, s.BatchCode, s.Movement.Quantity, s.Movement.CostPaise, s.Movement.OccurredAt))]);
    }

    public Task<bool> HasStockItemAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken = default)
        => db.Set<StockItem>().AnyAsync(s => s.FulfillmentLocationId == fulfillmentLocationId, cancellationToken);

    private static QuantityUnit ToUnit(StockUnit unit) => unit == StockUnit.Piece ? QuantityUnit.Piece : QuantityUnit.Gram;
}

/// <summary>
/// City readiness (LOC-R6): the city needs at least one stock item at one of its locations. Locations.Public has no
/// "locations of a city" read, so the check maps Inventory's own location ids to cities through
/// <see cref="ILocationsApi.GetFulfillmentLocationsAsync"/>.
/// </summary>
internal sealed class InventoryCityActivationCheck(FreshOrbitDbContext db, ILocationsApi locations) : ICityActivationCheck
{
    public async Task<IReadOnlyList<string>> GetMissingItemsAsync(Guid cityId, CancellationToken cancellationToken = default)
    {
        var locationIds = await db.Set<StockItem>().AsNoTracking().Select(s => s.FulfillmentLocationId).Distinct().ToListAsync(cancellationToken);
        if (locationIds.Count > 0)
        {
            var summaries = await locations.GetFulfillmentLocationsAsync(locationIds, cancellationToken);
            if (summaries.Any(l => l.CityId == cityId))
            {
                return [];
            }
        }

        return [InventoryErrors.StockItemMissing];
    }
}
