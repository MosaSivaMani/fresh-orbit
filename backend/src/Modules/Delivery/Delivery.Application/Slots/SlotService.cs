using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Delivery.Domain.Shared;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Delivery.Application.Slots;

/// <summary>PATCH body: new capacity and/or OPEN / CLOSED. At least one is required.</summary>
public sealed record ChangeSlotRequest(int? Capacity, SlotStatus? Status);

/// <summary>Manual slot changes (DEL-R3, §2 slot).</summary>
public sealed class SlotService(IDeliverySlotRepository slots, ILocationCalendar calendar, IUnitOfWork unitOfWork, ICurrentActor actor)
{
    public async Task<DeliverySlot> ChangeAsync(Guid slotId, ChangeSlotRequest request, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Capacity is null && request.Status is null)
        {
            throw DeliveryErrors.Invalid("capacity", "Send a capacity or a status to change.");
        }

        var slot = await slots.GetAsync(slotId, cancellationToken) ?? throw new NotFoundException("No delivery slot has this id.");
        actor.EnsureLocation(slot.FulfillmentLocationId);
        unitOfWork.SetExpectedVersion(slot, expectedVersion);
        if (request.Status == SlotStatus.Open && slot.Status == SlotStatus.Closed)
        {
            var closed = await calendar.GetClosedDatesAsync(slot.FulfillmentLocationId, slot.SlotDate, slot.SlotDate, cancellationToken);
            if (closed.Contains(slot.SlotDate))
            {
                throw new DomainException(DeliveryErrors.SlotNotAvailable, "The location is closed on this date; remove the closure first.", "status");
            }
        }

        if (request.Capacity is { } capacity)
        {
            slot.ChangeCapacity(capacity);
        }

        switch (request.Status)
        {
            case SlotStatus.Closed:
                slot.Close(SlotClosedReason.Manual);
                break;
            case SlotStatus.Open:
                slot.Reopen();
                break;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return slot;
    }
}
