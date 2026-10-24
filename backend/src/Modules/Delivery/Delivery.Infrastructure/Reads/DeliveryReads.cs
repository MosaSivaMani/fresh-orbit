using FreshOrbit.Delivery.Application.Reads;
using FreshOrbit.Delivery.Domain.Riders;
using FreshOrbit.Delivery.Domain.Shipments;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Domain.Templates;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Delivery.Infrastructure.Reads;

/// <summary>No-tracking list queries; one count + one page query each.</summary>
internal sealed class DeliveryReads(FreshOrbitDbContext db, IClock clock) : IDeliveryReads
{
    public async Task<Page<SlotTemplateResponse>> ListTemplatesAsync(TemplateQuery query, CancellationToken cancellationToken)
    {
        var source = db.Set<SlotTemplate>().AsNoTracking();
        if (query.FulfillmentLocationId is { } locationId)
        {
            source = source.Where(t => t.FulfillmentLocationId == locationId);
        }

        if (query.Locations is { } locations)
        {
            source = source.Where(t => locations.Contains(t.FulfillmentLocationId));
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderBy(t => t.FulfillmentLocationId).ThenBy(t => t.DayOfWeek).ThenBy(t => t.StartTime)
            .Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken);
        return new([.. items.Select(t => t.ToResponse())], total);
    }

    public async Task<Page<DeliverySlotResponse>> ListSlotsAsync(SlotQuery query, CancellationToken cancellationToken)
    {
        var source = db.Set<DeliverySlot>().AsNoTracking();
        if (query.FulfillmentLocationId is { } locationId)
        {
            source = source.Where(s => s.FulfillmentLocationId == locationId);
        }

        if (query.Locations is { } locations)
        {
            source = source.Where(s => locations.Contains(s.FulfillmentLocationId));
        }

        if (query.FromDate is { } fromDate)
        {
            source = source.Where(s => s.SlotDate >= fromDate);
        }

        if (query.ToDate is { } toDate)
        {
            source = source.Where(s => s.SlotDate <= toDate);
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderBy(s => s.StartAt).ThenBy(s => s.FulfillmentLocationId).Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        return new([.. items.Select(s => s.ToResponse(now))], total);
    }

    public async Task<Page<RiderResponse>> ListRidersAsync(RiderQuery query, CancellationToken cancellationToken)
    {
        var source = db.Set<Rider>().AsNoTracking();
        if (query.FulfillmentLocationId is { } locationId)
        {
            source = source.Where(r => r.FulfillmentLocationId == locationId);
        }

        if (query.Locations is { } locations)
        {
            source = source.Where(r => locations.Contains(r.FulfillmentLocationId));
        }

        if (query.IsActive is { } isActive)
        {
            source = source.Where(r => r.IsActive == isActive);
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderBy(r => r.FullName).ThenBy(r => r.Id).Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken);
        return new([.. items.Select(r => r.ToResponse())], total);
    }

    public async Task<Page<RiderLookupItem>> LookupRidersAsync(RiderLookupQuery query, CancellationToken cancellationToken)
    {
        var source = db.Set<Rider>().AsNoTracking();
        if (query.FulfillmentLocationId is { } locationId)
        {
            source = source.Where(r => r.FulfillmentLocationId == locationId);
        }

        if (query.Locations is { } locations)
        {
            source = source.Where(r => locations.Contains(r.FulfillmentLocationId));
        }

        if (query.IsActive is { } isActive)
        {
            source = source.Where(r => r.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = $"%{query.Q.Trim().Replace("%", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal)}%";
            source = source.Where(r => EF.Functions.ILike(r.FullName, pattern));
        }

        var total = await source.CountAsync(cancellationToken);
        var rows = await source.OrderBy(r => r.FullName).ThenBy(r => r.Id).Skip(query.Skip).Take(query.Take)
            .Select(r => new { r.Id, r.FulfillmentLocationId, r.FullName, r.Phone, r.IsActive })
            .ToListAsync(cancellationToken);
        return new([.. rows.Select(r => new RiderLookupItem(r.Id, r.FulfillmentLocationId, r.FullName, r.Phone.Length <= 4 ? r.Phone : r.Phone[^4..], r.IsActive))], total);
    }

    public async Task<Page<ShipmentResponse>> ListShipmentsAsync(ShipmentQuery query, CancellationToken cancellationToken)
    {
        var source = db.Set<Shipment>().AsNoTracking();
        if (query.FulfillmentLocationId is { } locationId)
        {
            source = source.Where(s => s.FulfillmentLocationId == locationId);
        }

        if (query.Locations is { } locations)
        {
            source = source.Where(s => locations.Contains(s.FulfillmentLocationId));
        }

        if (query.SlotId is { } slotId)
        {
            source = source.Where(s => s.DeliverySlotId == slotId);
        }

        if (query.RiderId is { } riderId)
        {
            source = source.Where(s => s.RiderId == riderId);
        }

        if (query.Statuses.Count > 0)
        {
            var statuses = query.Statuses.ToList();
            source = source.Where(s => statuses.Contains(s.Status));
        }

        if (query.SlotDate is { } slotDate)
        {
            var slots = db.Set<DeliverySlot>().AsNoTracking();
            source = source.Where(s => slots.Any(slot => slot.Id == s.DeliverySlotId && slot.SlotDate == slotDate));
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await Sorted(source, query.Sort).Include(s => s.Attempts)
            .Skip(query.Skip).Take(query.Take).AsSplitQuery().ToListAsync(cancellationToken);
        return new([.. items.Select(s => s.ToResponse())], total);
    }

    /// <summary>G10: the requested keys in order, then <c>placedAt</c> (when not requested) and the id as tie-breakers.</summary>
    private IQueryable<Shipment> Sorted(IQueryable<Shipment> source, IReadOnlyList<ShipmentSortKey> keys)
    {
        var slots = db.Set<DeliverySlot>().AsNoTracking();
        IOrderedQueryable<Shipment>? ordered = null;
        foreach (var key in keys)
        {
            ordered = key.Field switch
            {
                ShipmentSortField.SlotStartAt => Order(ordered, source, s => slots.Where(slot => slot.Id == s.DeliverySlotId).Select(slot => slot.StartAt).FirstOrDefault(), key.Descending),
                ShipmentSortField.Status => Order(ordered, source, s => s.Status, key.Descending),
                ShipmentSortField.DispatchedAt => Order(ordered, source, s => s.DispatchedAt, key.Descending),
                _ => Order(ordered, source, s => s.PlacedAt, key.Descending),
            };
        }

        if (!keys.Any(k => k.Field == ShipmentSortField.PlacedAt))
        {
            ordered = Order(ordered, source, s => s.PlacedAt, descending: false);
        }

        return ordered!.ThenBy(s => s.Id);
    }

    private static IOrderedQueryable<Shipment> Order<TKey>(IOrderedQueryable<Shipment>? ordered, IQueryable<Shipment> source, System.Linq.Expressions.Expression<Func<Shipment, TKey>> key, bool descending)
        => ordered is null
            ? (descending ? source.OrderByDescending(key) : source.OrderBy(key))
            : (descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key));

    public async Task<ShipmentResponse?> GetShipmentAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var shipment = await db.Set<Shipment>().AsNoTracking().Include(s => s.Attempts).FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);
        return shipment?.ToResponse();
    }

    public async Task<IReadOnlyList<CustomerSlotResponse>> ListCustomerSlotsAsync(Guid fulfillmentLocationId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var slots = await db.Set<DeliverySlot>().AsNoTracking()
            .Where(s => s.FulfillmentLocationId == fulfillmentLocationId && s.SlotDate >= fromDate && s.SlotDate <= toDate)
            .OrderBy(s => s.StartAt)
            .ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        return [.. slots.Select(s => new CustomerSlotResponse(s.Id, s.SlotDate, s.StartAt, s.EndAt, s.CutoffAt, s.GetDisplayStatus(now)))];
    }
}
