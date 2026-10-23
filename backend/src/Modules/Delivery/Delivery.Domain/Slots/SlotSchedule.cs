using FreshOrbit.Delivery.Domain.Templates;

namespace FreshOrbit.Delivery.Domain.Slots;

/// <summary>
/// Slot times and generation (DEL-R9), pure functions of the city time zone and the clock.
/// Template times and <c>slot_date</c> are local; stored instants are UTC.
/// </summary>
public static class SlotSchedule
{
    /// <summary>Dates generated and listed: today … today + 6 (local).</summary>
    public const int HorizonDays = 7;

    public static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo timeZone)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);

    /// <summary>ISO day of week (1 = Monday … 7 = Sunday).</summary>
    public static int IsoDayOfWeek(DateOnly date) => ((int)date.DayOfWeek + 6) % 7 + 1;

    /// <summary>UTC instant of a local date and time; null when the local time does not exist (DST gap).</summary>
    public static DateTimeOffset? ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
        {
            return null;
        }

        return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>The local dates of the horizon starting at today (local).</summary>
    public static IReadOnlyList<DateOnly> HorizonDates(DateTimeOffset now, TimeZoneInfo timeZone)
    {
        var today = LocalDate(now, timeZone);
        return [.. Enumerable.Range(0, HorizonDays).Select(today.AddDays)];
    }

    /// <summary>Times of a template on a local date: start, end and cutoff (start − cutoff minutes); null in a DST gap.</summary>
    public static (DateTimeOffset StartAt, DateTimeOffset EndAt, DateTimeOffset CutoffAt)? TimesFor(SlotTemplate template, DateOnly date, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(template);
        var startAt = ToUtc(date, template.StartTime, timeZone);
        var endAt = ToUtc(date, template.EndTime, timeZone);
        if (startAt is null || endAt is null || endAt <= startAt)
        {
            return null;
        }

        return (startAt.Value, endAt.Value, startAt.Value.AddMinutes(-template.CutoffMinutes));
    }

    /// <summary>
    /// DEL-R9: the slots missing at a location. Per local horizon date that is not closed, per active template of the
    /// date's ISO day: skip when the cutoff has passed or a slot with the same start already exists.
    /// </summary>
    public static IReadOnlyList<DeliverySlot> Generate(
        Guid fulfillmentLocationId,
        TimeZoneInfo timeZone,
        IEnumerable<SlotTemplate> templates,
        IReadOnlySet<DateOnly> closedDates,
        IReadOnlySet<DateTimeOffset> existingStartAts,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(closedDates);
        ArgumentNullException.ThrowIfNull(existingStartAts);
        var active = templates.Where(t => t.IsActive && t.FulfillmentLocationId == fulfillmentLocationId).ToList();
        var created = new List<DeliverySlot>();
        var seen = new HashSet<DateTimeOffset>(existingStartAts);
        foreach (var date in HorizonDates(now, timeZone))
        {
            if (closedDates.Contains(date))
            {
                continue;
            }

            var day = IsoDayOfWeek(date);
            foreach (var template in active.Where(t => t.DayOfWeek == day).OrderBy(t => t.StartTime))
            {
                if (TimesFor(template, date, timeZone) is not { } times || times.CutoffAt <= now || !seen.Add(times.StartAt))
                {
                    continue;
                }

                created.Add(DeliverySlot.Create(fulfillmentLocationId, template.Id, date, times.StartAt, times.EndAt, times.CutoffAt, template.Capacity));
            }
        }

        return created;
    }
}
