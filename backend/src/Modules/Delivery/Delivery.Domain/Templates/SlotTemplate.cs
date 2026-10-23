using FreshOrbit.Delivery.Domain.Shared;
using FreshOrbit.Delivery.Domain.Templates.Events;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Delivery.Domain.Templates;

/// <summary>
/// A weekly delivery window of a fulfillment location (times in the city time zone, ISO day 1 = Monday).
/// Slots are generated from active templates (DEL-R9); template changes reach slots through
/// <see cref="SlotTemplateChanged"/> (DEL-R12).
/// </summary>
public sealed class SlotTemplate : AggregateRoot
{
    public const int MinCapacity = 1;
    public const int MaxCapacity = 500;
    public const int MaxCutoffMinutes = 2880;
    public const int TimeStepMinutes = 15;

    private SlotTemplate()
    {
    }

    private SlotTemplate(Guid fulfillmentLocationId)
    {
        FulfillmentLocationId = fulfillmentLocationId;
    }

    public Guid FulfillmentLocationId { get; private set; }

    /// <summary>ISO day of week: 1 = Monday … 7 = Sunday.</summary>
    public int DayOfWeek { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public int Capacity { get; private set; }

    public int CutoffMinutes { get; private set; }

    public bool IsActive { get; private set; }

    public static SlotTemplate Create(Guid fulfillmentLocationId, int dayOfWeek, TimeOnly startTime, TimeOnly endTime, int capacity, int cutoffMinutes)
    {
        Validate(dayOfWeek, startTime, endTime, capacity, cutoffMinutes);
        var template = new SlotTemplate(fulfillmentLocationId);
        template.Apply(dayOfWeek, startTime, endTime, capacity, cutoffMinutes, isActive: true);
        return template;
    }

    public void Update(int dayOfWeek, TimeOnly startTime, TimeOnly endTime, int capacity, int cutoffMinutes, bool isActive)
    {
        Validate(dayOfWeek, startTime, endTime, capacity, cutoffMinutes);
        Apply(dayOfWeek, startTime, endTime, capacity, cutoffMinutes, isActive);
    }

    /// <summary>True when this active template shares a day and part of <c>[start, end)</c> with the given window (DEL-R2).</summary>
    public bool Overlaps(int dayOfWeek, TimeOnly startTime, TimeOnly endTime)
        => IsActive && DayOfWeek == dayOfWeek && startTime < EndTime && StartTime < endTime;

    /// <summary>DEL-R1. Throws <see cref="RefusalException"/> listing every invalid field.</summary>
    public static void Validate(int dayOfWeek, TimeOnly startTime, TimeOnly endTime, int capacity, int cutoffMinutes)
    {
        var errors = new List<FieldError>();
        if (dayOfWeek is < 1 or > 7)
        {
            errors.Add(new FieldError("dayOfWeek", DeliveryErrors.ValidationFailed, "Day of week must be between 1 (Monday) and 7 (Sunday)."));
        }

        if (!IsOnStep(startTime))
        {
            errors.Add(new FieldError("startTime", DeliveryErrors.ValidationFailed, "Start time must be HH:mm on a 15-minute boundary."));
        }

        if (!IsOnStep(endTime))
        {
            errors.Add(new FieldError("endTime", DeliveryErrors.ValidationFailed, "End time must be HH:mm on a 15-minute boundary."));
        }
        else if (endTime <= startTime)
        {
            errors.Add(new FieldError("endTime", DeliveryErrors.ValidationFailed, "End time must be after the start time on the same day."));
        }

        if (capacity is < MinCapacity or > MaxCapacity)
        {
            errors.Add(new FieldError("capacity", DeliveryErrors.ValidationFailed, "Capacity must be between 1 and 500."));
        }

        if (cutoffMinutes is < 0 or > MaxCutoffMinutes)
        {
            errors.Add(new FieldError("cutoffMinutes", DeliveryErrors.ValidationFailed, "Cutoff minutes must be between 0 and 2880."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }
    }

    private static bool IsOnStep(TimeOnly time) => time.Second == 0 && time.Millisecond == 0 && time.Microsecond == 0 && time.Minute % TimeStepMinutes == 0;

    private void Apply(int dayOfWeek, TimeOnly startTime, TimeOnly endTime, int capacity, int cutoffMinutes, bool isActive)
    {
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        Capacity = capacity;
        CutoffMinutes = cutoffMinutes;
        IsActive = isActive;
        Raise(new SlotTemplateChanged(Id, FulfillmentLocationId, isActive));
    }
}
