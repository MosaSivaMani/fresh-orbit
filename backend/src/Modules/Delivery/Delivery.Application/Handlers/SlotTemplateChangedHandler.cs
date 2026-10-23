using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Delivery.Application.Slots;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Domain.Templates;
using FreshOrbit.Delivery.Domain.Templates.Events;
using FreshOrbit.SharedKernel.Events;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Delivery.Application.Handlers;

/// <summary>
/// DEL-R12: future slots of a changed template. Unbooked: times, cutoff and capacity rewritten (a collision with
/// another slot's start closes it TEMPLATE_INACTIVE); booked: times kept, capacity = max(new, booked). A deactivated
/// template closes its unbooked future slots and freezes booked ones at capacity = booked. Then DEL-R9 runs.
/// </summary>
public sealed class SlotTemplateChangedHandler(
    ISlotTemplateRepository templates,
    IDeliverySlotRepository slots,
    ILocationCalendar calendar,
    SlotGenerationService generation,
    IClock clock) : IEventHandler<SlotTemplateChanged>
{
    public async Task HandleAsync(SlotTemplateChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var template = await templates.GetAsync(domainEvent.SlotTemplateId, cancellationToken);
        if (template is null)
        {
            return;
        }

        var locations = await calendar.GetLocationsAsync([template.FulfillmentLocationId], cancellationToken);
        var location = locations.FirstOrDefault(l => l.Id == template.FulfillmentLocationId);
        if (location is null)
        {
            return;
        }

        var now = clock.UtcNow;
        var future = await slots.GetFutureByLocationAsync(template.FulfillmentLocationId, now, cancellationToken);
        ApplyTemplate(template, future, location.TimeZone());
        await generation.TrackMissingSlotsAsync(template.FulfillmentLocationId, cancellationToken);
    }

    /// <summary>Rewrites the template's slots among <paramref name="futureSlotsOfLocation"/> (all future slots of the location).</summary>
    public static void ApplyTemplate(SlotTemplate template, IReadOnlyList<DeliverySlot> futureSlotsOfLocation, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(futureSlotsOfLocation);
        foreach (var slot in futureSlotsOfLocation.Where(s => s.SlotTemplateId == template.Id))
        {
            if (slot.BookedCount > 0)
            {
                slot.LimitCapacity(template.IsActive ? template.Capacity : slot.BookedCount);
                continue;
            }

            var times = template.IsActive && SlotSchedule.IsoDayOfWeek(slot.SlotDate) == template.DayOfWeek
                ? SlotSchedule.TimesFor(template, slot.SlotDate, timeZone)
                : null;
            if (times is null)
            {
                slot.Close(SlotClosedReason.TemplateInactive);
                continue;
            }

            var (startAt, endAt, cutoffAt) = times.Value;
            if (futureSlotsOfLocation.Any(other => other.Id != slot.Id && other.StartAt == startAt))
            {
                slot.Close(SlotClosedReason.TemplateInactive);
                continue;
            }

            slot.Reschedule(startAt, endAt, cutoffAt, template.Capacity);
            if (slot.ClosedReason == SlotClosedReason.TemplateInactive)
            {
                slot.Reopen();
            }
        }
    }
}
