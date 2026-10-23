using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Delivery.Domain.Riders;
using FreshOrbit.Delivery.Domain.Shared;
using FreshOrbit.Delivery.Domain.Shipments;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Domain.Templates;
using FreshOrbit.Delivery.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Delivery.Infrastructure.Public;

/// <summary><see cref="IDeliveryApi"/> over the shared DbContext. <see cref="BookSlotAsync"/> only tracks.</summary>
internal sealed class DeliveryApi(FreshOrbitDbContext db, IClock clock) : IDeliveryApi
{
    public async Task<IReadOnlyList<SlotInfo>> GetOpenSlotsAsync(Guid fulfillmentLocationId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        return await db.Set<DeliverySlot>().AsNoTracking()
            .Where(s => s.FulfillmentLocationId == fulfillmentLocationId && s.Status == SlotStatus.Open
                && s.StartAt >= from && s.StartAt < until && s.CutoffAt > now && s.BookedCount < s.Capacity)
            .OrderBy(s => s.StartAt)
            .Select(s => new SlotInfo(s.Id, s.StartAt, s.EndAt, s.CutoffAt, s.Capacity - s.BookedCount))
            .ToListAsync(cancellationToken);
    }

    public async Task<SlotBooking> BookSlotAsync(Guid slotId, Guid fulfillmentLocationId, CancellationToken cancellationToken = default)
    {
        var slot = await db.Set<DeliverySlot>().FirstOrDefaultAsync(s => s.Id == slotId, cancellationToken);
        if (slot is null)
        {
            return new SlotBooking(false, slotId, null, null, DeliveryErrors.SlotNotAvailable);
        }

        var refusal = slot.GetBookingRefusal(fulfillmentLocationId, clock.UtcNow);
        if (refusal is not null)
        {
            return new SlotBooking(false, slotId, slot.StartAt, slot.EndAt, refusal);
        }

        slot.Book(fulfillmentLocationId, clock.UtcNow);
        return new SlotBooking(true, slotId, slot.StartAt, slot.EndAt, null);
    }

    public async Task<SlotView?> GetSlotAsync(Guid slotId, Guid fulfillmentLocationId, CancellationToken cancellationToken = default)
    {
        var slot = await db.Set<DeliverySlot>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == slotId && s.FulfillmentLocationId == fulfillmentLocationId, cancellationToken);
        return slot is null
            ? null
            : new SlotView(slot.Id, slot.FulfillmentLocationId, slot.SlotDate, slot.StartAt, slot.EndAt, slot.CutoffAt, slot.Capacity, slot.BookedCount, slot.GetDisplayStatus(clock.UtcNow).ToCode());
    }

    public async Task<ShipmentTracking?> GetShipmentForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var shipment = await db.Set<Shipment>().AsNoTracking().Include(s => s.Attempts).FirstOrDefaultAsync(s => s.OrderId == orderId, cancellationToken);
        if (shipment is null)
        {
            return null;
        }

        var rider = shipment.RiderId is { } riderId
            ? await db.Set<Rider>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == riderId, cancellationToken)
            : null;
        return new ShipmentTracking(
            shipment.Id,
            shipment.OrderId,
            shipment.Status.ToCode(),
            rider?.Id,
            rider?.FullName,
            rider?.Phone,
            shipment.DispatchedAt,
            shipment.DeliveredAt,
            shipment.Status == ShipmentStatus.OutForDelivery ? shipment.DeliveryCode : null,
            [.. shipment.Attempts.OrderBy(a => a.AttemptNo).Select(a => new ShipmentAttemptInfo(a.AttemptNo, a.Outcome.ToCode(), a.AttemptedAt))]);
    }

    public async Task<ShipmentInfo?> GetShipmentAsync(Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var s = await db.Set<Shipment>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == shipmentId, cancellationToken);
        return s is null ? null : new ShipmentInfo(s.Id, s.OrderId, s.FulfillmentLocationId, s.DeliverySlotId, s.RiderId, s.Status.ToCode(), s.DeliveredAt, s.CashCollectedPaise);
    }

    public Task<bool> HasActiveSlotTemplateAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken = default)
        => db.Set<SlotTemplate>().AnyAsync(t => t.FulfillmentLocationId == fulfillmentLocationId && t.IsActive, cancellationToken);
}

/// <summary><see cref="INextSlotProvider"/> for <c>GET /serviceability</c>: earliest bookable slot.</summary>
internal sealed class DeliveryNextSlotProvider(FreshOrbitDbContext db, IClock clock) : INextSlotProvider
{
    public async Task<NextSlot?> GetNextOpenSlotAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        return await db.Set<DeliverySlot>().AsNoTracking()
            .Where(s => s.FulfillmentLocationId == fulfillmentLocationId && s.Status == SlotStatus.Open && s.CutoffAt > now && s.BookedCount < s.Capacity)
            .OrderBy(s => s.StartAt)
            .Select(s => new NextSlot(s.Id, s.StartAt, s.EndAt, s.CutoffAt))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

/// <summary>
/// City readiness (LOC-R6): the city needs a location with an active slot template, else <c>SLOT_TEMPLATE_MISSING</c>.
/// The city's locations are found from the template locations' <c>CityId</c> (Locations.Public has no
/// locations-by-city read).
/// </summary>
internal sealed class DeliveryCityActivationCheck(FreshOrbitDbContext db, ILocationCalendar calendar) : ICityActivationCheck
{
    public async Task<IReadOnlyList<string>> GetMissingItemsAsync(Guid cityId, CancellationToken cancellationToken = default)
    {
        var locationIds = await db.Set<SlotTemplate>().AsNoTracking().Where(t => t.IsActive).Select(t => t.FulfillmentLocationId).Distinct().ToListAsync(cancellationToken);
        if (locationIds.Count > 0)
        {
            var locations = await calendar.GetLocationsAsync(locationIds, cancellationToken);
            if (locations.Any(l => l.CityId == cityId))
            {
                return [];
            }
        }

        return [DeliveryErrors.SlotTemplateMissing];
    }
}
