using FreshOrbit.Delivery.Domain.Shared;
using FreshOrbit.Delivery.Domain.Slots.Events;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Delivery.Domain.Slots;

/// <summary>Stored slot status: OPEN / CLOSED.</summary>
public enum SlotStatus
{
    Open,
    Closed,
}

/// <summary>Why a slot is CLOSED: MANUAL / CLOSURE / TEMPLATE_INACTIVE.</summary>
public enum SlotClosedReason
{
    Manual,
    Closure,
    TemplateInactive,
}

/// <summary>What a customer sees (computed, never stored): AVAILABLE / FULL / CUTOFF_PASSED / CLOSED.</summary>
public enum SlotDisplayStatus
{
    Available,
    Full,
    CutoffPassed,
    Closed,
}

/// <summary>
/// One bookable delivery window on a local date. Booked concurrently at checkout, so it locks alone (its own
/// <c>xmin</c>). <c>0 &lt;= booked_count &lt;= capacity</c> is also a DB check (DEL-I1).
/// </summary>
public sealed class DeliverySlot : AggregateRoot
{
    public const int MaxCapacity = 500;

    private DeliverySlot()
    {
    }

    private DeliverySlot(Guid fulfillmentLocationId, Guid? slotTemplateId, DateOnly slotDate, DateTimeOffset startAt, DateTimeOffset endAt, DateTimeOffset cutoffAt, int capacity)
    {
        FulfillmentLocationId = fulfillmentLocationId;
        SlotTemplateId = slotTemplateId;
        SlotDate = slotDate;
        StartAt = startAt;
        EndAt = endAt;
        CutoffAt = cutoffAt;
        Capacity = capacity;
        Status = SlotStatus.Open;
    }

    public Guid FulfillmentLocationId { get; private set; }

    public Guid? SlotTemplateId { get; private set; }

    /// <summary>Local date in the city time zone.</summary>
    public DateOnly SlotDate { get; private set; }

    public DateTimeOffset StartAt { get; private set; }

    public DateTimeOffset EndAt { get; private set; }

    public DateTimeOffset CutoffAt { get; private set; }

    public int Capacity { get; private set; }

    public int BookedCount { get; private set; }

    public SlotStatus Status { get; private set; }

    public SlotClosedReason? ClosedReason { get; private set; }

    public static DeliverySlot Create(Guid fulfillmentLocationId, Guid? slotTemplateId, DateOnly slotDate, DateTimeOffset startAt, DateTimeOffset endAt, DateTimeOffset cutoffAt, int capacity)
    {
        EnsureTimes(startAt, endAt, cutoffAt);
        if (capacity is < 0 or > MaxCapacity)
        {
            throw DeliveryErrors.Invalid("capacity", "Capacity must be between 0 and 500.");
        }

        return new DeliverySlot(fulfillmentLocationId, slotTemplateId, slotDate, startAt, endAt, cutoffAt, capacity);
    }

    /// <summary>Displayed status, precedence CLOSED &gt; CUTOFF_PASSED &gt; FULL &gt; AVAILABLE (§2).</summary>
    public SlotDisplayStatus GetDisplayStatus(DateTimeOffset now)
    {
        if (Status == SlotStatus.Closed)
        {
            return SlotDisplayStatus.Closed;
        }

        if (now >= CutoffAt)
        {
            return SlotDisplayStatus.CutoffPassed;
        }

        return BookedCount >= Capacity ? SlotDisplayStatus.Full : SlotDisplayStatus.Available;
    }

    public int Remaining => Math.Max(0, Capacity - BookedCount);

    /// <summary>DEL-R10 checks in order; null when the slot can be booked.</summary>
    public string? GetBookingRefusal(Guid fulfillmentLocationId, DateTimeOffset now)
    {
        if (Status != SlotStatus.Open || FulfillmentLocationId != fulfillmentLocationId)
        {
            return DeliveryErrors.SlotNotAvailable;
        }

        if (now >= CutoffAt)
        {
            return DeliveryErrors.SlotCutoffPassed;
        }

        return BookedCount >= Capacity ? DeliveryErrors.SlotFull : null;
    }

    /// <summary>DEL-R10: takes one unit of capacity or throws with the refusal code.</summary>
    public void Book(Guid fulfillmentLocationId, DateTimeOffset now)
    {
        var refusal = GetBookingRefusal(fulfillmentLocationId, now);
        if (refusal is not null)
        {
            throw new DomainException(refusal, "The delivery slot cannot be booked; choose another slot.", "deliverySlotId");
        }

        BookedCount++;
    }

    /// <summary>DEL-R11: frees one unit whatever the time or status, never below zero.</summary>
    public void Release() => BookedCount = Math.Max(0, BookedCount - 1);

    /// <summary>DEL-R3.</summary>
    public void ChangeCapacity(int capacity)
    {
        if (capacity is < 0 or > MaxCapacity)
        {
            throw DeliveryErrors.Invalid("capacity", "Capacity must be between 0 and 500.");
        }

        if (capacity < BookedCount)
        {
            throw new DomainException(DeliveryErrors.SlotCapacityBelowBooked, "Capacity must not be below the number of booked orders.", "capacity");
        }

        Capacity = capacity;
    }

    /// <summary>OPEN → CLOSED. Raises <see cref="SlotClosedWithBookings"/> when orders are booked. False when already closed.</summary>
    public bool Close(SlotClosedReason reason)
    {
        if (Status == SlotStatus.Closed)
        {
            return false;
        }

        Status = SlotStatus.Closed;
        ClosedReason = reason;
        if (BookedCount > 0)
        {
            Raise(new SlotClosedWithBookings(Id, FulfillmentLocationId, StartAt, BookedCount, reason));
        }

        return true;
    }

    /// <summary>CLOSED → OPEN. False when already open.</summary>
    public bool Reopen()
    {
        if (Status == SlotStatus.Open)
        {
            return false;
        }

        Status = SlotStatus.Open;
        ClosedReason = null;
        return true;
    }

    /// <summary>DEL-R12 for an unbooked slot: times, cutoff and capacity from the template.</summary>
    public void Reschedule(DateTimeOffset startAt, DateTimeOffset endAt, DateTimeOffset cutoffAt, int capacity)
    {
        EnsureTimes(startAt, endAt, cutoffAt);
        StartAt = startAt;
        EndAt = endAt;
        CutoffAt = cutoffAt;
        Capacity = Math.Max(capacity, BookedCount);
    }

    /// <summary>DEL-R12 for a booked slot: capacity = max(capacity, booked), times kept.</summary>
    public void LimitCapacity(int capacity) => Capacity = Math.Max(capacity, BookedCount);

    private static void EnsureTimes(DateTimeOffset startAt, DateTimeOffset endAt, DateTimeOffset cutoffAt)
    {
        if (startAt >= endAt || cutoffAt > startAt)
        {
            throw new ArgumentException("A slot needs start < end and cutoff <= start (DEL-I2).");
        }
    }
}
