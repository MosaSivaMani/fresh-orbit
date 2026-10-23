using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Delivery.Domain.Slots.Events;

/// <summary>A slot with booked orders was closed (closure, manual or template); operations must contact customers.</summary>
public sealed record SlotClosedWithBookings(Guid DeliverySlotId, Guid FulfillmentLocationId, DateTimeOffset StartAt, int BookedCount, SlotClosedReason Reason) : DomainEvent;
