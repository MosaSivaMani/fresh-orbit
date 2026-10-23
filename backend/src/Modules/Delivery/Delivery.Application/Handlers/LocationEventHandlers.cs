using FreshOrbit.Delivery.Application.Slots;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Delivery.Application.Handlers;

/// <summary>
/// Delivery's reaction to Locations events (DEL-R13, location activation). Tracking only; state-based, so a re-run
/// is a no-op. Thin <c>IEventHandler</c> adapters map <c>LocationClosureAdded</c> / <c>LocationClosureRemoved</c> /
/// <c>FulfillmentLocationActivated</c> to these methods.
/// </summary>
public sealed class LocationEventHandlers(IDeliverySlotRepository slots, SlotGenerationService generation, IClock clock)
{
    /// <summary>OPEN slots of the local date → CLOSED (CLOSURE); booked ones raise <c>SlotClosedWithBookings</c>.</summary>
    public async Task OnClosureAddedAsync(Guid fulfillmentLocationId, DateOnly closedOn, CancellationToken cancellationToken)
    {
        foreach (var slot in await slots.GetByLocalDateAsync(fulfillmentLocationId, closedOn, cancellationToken))
        {
            slot.Close(SlotClosedReason.Closure);
        }
    }

    /// <summary>CLOSURE-closed future slots of the date → OPEN, then missing slots are generated (DEL-R9).</summary>
    public async Task OnClosureRemovedAsync(Guid fulfillmentLocationId, DateOnly closedOn, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        foreach (var slot in await slots.GetByLocalDateAsync(fulfillmentLocationId, closedOn, cancellationToken))
        {
            if (slot.Status == SlotStatus.Closed && slot.ClosedReason == SlotClosedReason.Closure && slot.StartAt > now)
            {
                slot.Reopen();
            }
        }

        await generation.TrackMissingSlotsAsync(fulfillmentLocationId, cancellationToken);
    }

    /// <summary><c>FulfillmentLocationActivated</c>: generate its slots.</summary>
    public Task OnLocationActivatedAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken)
        => generation.TrackMissingSlotsAsync(fulfillmentLocationId, cancellationToken);
}
