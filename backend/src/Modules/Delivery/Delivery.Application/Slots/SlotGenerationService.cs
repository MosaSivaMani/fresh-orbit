using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Domain.Templates;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Delivery.Application.Slots;

/// <summary>
/// DEL-R9 for one location: reads templates, closures and the horizon's existing slots once, tracks the missing
/// slots. Never saves (the job saves once per location; handlers are saved by the outbox dispatcher).
/// </summary>
public sealed class SlotGenerationService(ISlotTemplateRepository templates, IDeliverySlotRepository slots, ILocationCalendar calendar, IClock clock)
{
    /// <summary>Locations that have an active template (only they can need slots).</summary>
    public Task<IReadOnlyList<Guid>> GetCandidateLocationIdsAsync(CancellationToken cancellationToken)
        => templates.GetLocationIdsWithActiveTemplatesAsync(cancellationToken);

    /// <summary>Tracks the missing slots of an ACTIVE location; returns how many were added (0 = nothing to save).</summary>
    public async Task<int> TrackMissingSlotsAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken)
    {
        var location = (await calendar.GetLocationsAsync([fulfillmentLocationId], cancellationToken)).FirstOrDefault(l => l.Id == fulfillmentLocationId);
        if (location is not { Status: FulfillmentLocationStatus.Active })
        {
            return 0;
        }

        var active = await templates.GetActiveByLocationAsync(fulfillmentLocationId, cancellationToken);
        if (active.Count == 0)
        {
            return 0;
        }

        var now = clock.UtcNow;
        var timeZone = location.TimeZone();
        var dates = SlotSchedule.HorizonDates(now, timeZone);
        var from = SlotSchedule.ToUtc(dates[0], TimeOnly.MinValue, timeZone) ?? now;
        var until = from.AddDays(SlotSchedule.HorizonDays + 1);
        var existing = await slots.GetStartingBetweenAsync(fulfillmentLocationId, from.AddDays(-1), until, cancellationToken);
        var closed = await calendar.GetClosedDatesAsync(fulfillmentLocationId, dates[0], dates[^1], cancellationToken);
        var created = SlotSchedule.Generate(fulfillmentLocationId, timeZone, active, closed, existing.Select(s => s.StartAt).ToHashSet(), now);
        slots.AddRange(created);
        return created.Count;
    }
}
