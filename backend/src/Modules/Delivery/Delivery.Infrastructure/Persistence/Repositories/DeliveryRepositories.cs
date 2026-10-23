using FreshOrbit.Delivery.Domain.Riders;
using FreshOrbit.Delivery.Domain.Shipments;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Domain.Templates;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Delivery.Infrastructure.Persistence.Repositories;

internal sealed class SlotTemplateRepository(FreshOrbitDbContext db) : ISlotTemplateRepository
{
    public Task<SlotTemplate?> GetAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<SlotTemplate>().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SlotTemplate>> GetActiveByLocationAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken)
        => await db.Set<SlotTemplate>().Where(t => t.FulfillmentLocationId == fulfillmentLocationId && t.IsActive).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetLocationIdsWithActiveTemplatesAsync(CancellationToken cancellationToken)
        => await db.Set<SlotTemplate>().AsNoTracking().Where(t => t.IsActive).Select(t => t.FulfillmentLocationId).Distinct().ToListAsync(cancellationToken);

    public void Add(SlotTemplate slotTemplate) => db.Add(slotTemplate);
}

internal sealed class DeliverySlotRepository(FreshOrbitDbContext db) : IDeliverySlotRepository
{
    public Task<DeliverySlot?> GetAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<DeliverySlot>().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DeliverySlot>> GetStartingBetweenAsync(Guid fulfillmentLocationId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
        => await db.Set<DeliverySlot>()
            .Where(s => s.FulfillmentLocationId == fulfillmentLocationId && s.StartAt >= from && s.StartAt < until)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DeliverySlot>> GetByLocalDateAsync(Guid fulfillmentLocationId, DateOnly slotDate, CancellationToken cancellationToken)
        => await db.Set<DeliverySlot>().Where(s => s.FulfillmentLocationId == fulfillmentLocationId && s.SlotDate == slotDate).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DeliverySlot>> GetFutureByLocationAsync(Guid fulfillmentLocationId, DateTimeOffset after, CancellationToken cancellationToken)
        => await db.Set<DeliverySlot>().Where(s => s.FulfillmentLocationId == fulfillmentLocationId && s.StartAt > after).ToListAsync(cancellationToken);

    public void AddRange(IEnumerable<DeliverySlot> slots) => db.AddRange(slots);
}

internal sealed class RiderRepository(FreshOrbitDbContext db) : IRiderRepository
{
    public Task<Rider?> GetAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<Rider>().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> ActivePhoneExistsAsync(string phone, Guid? exceptRiderId, CancellationToken cancellationToken)
        => db.Set<Rider>().AnyAsync(r => r.IsActive && r.Phone == phone && r.Id != exceptRiderId, cancellationToken);

    public void Add(Rider rider) => db.Add(rider);
}

internal sealed class ShipmentRepository(FreshOrbitDbContext db) : IShipmentRepository
{
    public Task<Shipment?> GetAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<Shipment>().Include(s => s.Attempts).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Shipment?> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken)
        => db.Set<Shipment>().Include(s => s.Attempts).FirstOrDefaultAsync(s => s.OrderId == orderId, cancellationToken);

    public Task<int> CountOpenForRiderAsync(Guid riderId, CancellationToken cancellationToken)
        => db.Set<Shipment>().CountAsync(s => s.RiderId == riderId && (s.Status == ShipmentStatus.Assigned || s.Status == ShipmentStatus.OutForDelivery || s.Status == ShipmentStatus.Failed), cancellationToken);

    public void Add(Shipment shipment) => db.Add(shipment);
}
