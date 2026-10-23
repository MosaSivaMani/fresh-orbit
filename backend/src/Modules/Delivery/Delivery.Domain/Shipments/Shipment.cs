using System.Security.Cryptography;
using System.Text;
using FreshOrbit.Delivery.Domain.Riders;
using FreshOrbit.Delivery.Domain.Shared;
using FreshOrbit.Delivery.Public.Events;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Delivery.Domain.Shipments;

/// <summary>PENDING / ASSIGNED / OUT_FOR_DELIVERY / DELIVERED / FAILED / RETURNED / CANCELLED.</summary>
public enum ShipmentStatus
{
    Pending,
    Assigned,
    OutForDelivery,
    Delivered,
    Failed,
    Returned,
    Cancelled,
}

/// <summary>DELIVERED / CUSTOMER_UNAVAILABLE / ADDRESS_NOT_FOUND / REFUSED / OTHER.</summary>
public enum AttemptOutcome
{
    Delivered,
    CustomerUnavailable,
    AddressNotFound,
    Refused,
    Other,
}

/// <summary>CASH / UPI.</summary>
public enum CashMode
{
    Cash,
    Upi,
}

/// <summary>What the rider reports collecting at the door (DEL-R18: recorded, never compared with the amount due).</summary>
public sealed record CashReceipt(long AmountPaise, CashMode Mode, string? UpiReference);

/// <summary>Result of a delivery-code check.</summary>
public enum DeliverOutcome
{
    Delivered,
    CodeInvalid,
}

/// <summary>
/// The delivery of one order (DEL-I3: one per order). Attempts are insert-only children numbered 1, 2, … .
/// </summary>
public sealed class Shipment : AggregateRoot
{
    public const int MaxDeliveryCodeFailures = 5;
    public const decimal MaxSafeTemperatureC = 4.0m;
    public const decimal MinTemperatureC = -40.0m;
    public const decimal MaxTemperatureC = 40.0m;
    public const int MaxNotesLength = 500;
    public const int MaxUpiReferenceLength = 100;
    public const int DeliveryCodeLength = 4;

    private readonly List<DeliveryAttempt> _attempts = [];

    private Shipment()
    {
    }

    private Shipment(Guid orderId, Guid fulfillmentLocationId, Guid deliverySlotId, DateTimeOffset placedAt, ShipmentStatus status)
    {
        OrderId = orderId;
        FulfillmentLocationId = fulfillmentLocationId;
        DeliverySlotId = deliverySlotId;
        PlacedAt = placedAt;
        Status = status;
    }

    public Guid OrderId { get; private set; }

    public Guid FulfillmentLocationId { get; private set; }

    public Guid DeliverySlotId { get; private set; }

    public Guid? RiderId { get; private set; }

    public ShipmentStatus Status { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    public DateTimeOffset? PackedAt { get; private set; }

    public bool ColdBoxUsed { get; private set; }

    public decimal? DispatchTemperatureC { get; private set; }

    public bool TemperatureWarning { get; private set; }

    public DateTimeOffset? DispatchedAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public DateTimeOffset? ReturnedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Plain 4-digit code (D12), generated at dispatch and kept across reattempts.</summary>
    public string? DeliveryCode { get; private set; }

    public int DeliveryCodeFailures { get; private set; }

    public long? CashCollectedPaise { get; private set; }

    public CashMode? CashMode { get; private set; }

    public string? UpiReference { get; private set; }

    public DateTimeOffset? CashCollectedAt { get; private set; }

    public IReadOnlyList<DeliveryAttempt> Attempts => _attempts;

    public bool IsDeliveryCodeLocked => DeliveryCodeFailures >= MaxDeliveryCodeFailures;

    /// <summary>DEL-R15: max(attempt_no) + 1.</summary>
    public int NextAttemptNo => _attempts.Count == 0 ? 1 : _attempts.Max(a => a.AttemptNo) + 1;

    public static Shipment Create(Guid orderId, Guid fulfillmentLocationId, Guid deliverySlotId, DateTimeOffset placedAt)
        => new(orderId, fulfillmentLocationId, deliverySlotId, placedAt, ShipmentStatus.Pending);

    /// <summary>For an <c>OrderCancelled</c> that arrives before <c>OrderPlaced</c>: a late placement then no-ops.</summary>
    public static Shipment CreateCancelled(Guid orderId, Guid fulfillmentLocationId, Guid deliverySlotId, DateTimeOffset cancelledAt)
    {
        var shipment = new Shipment(orderId, fulfillmentLocationId, deliverySlotId, cancelledAt, ShipmentStatus.Cancelled) { CancelledAt = cancelledAt };
        return shipment;
    }

    /// <summary>PENDING / ASSIGNED → CANCELLED. False (no change) in any other status.</summary>
    public bool Cancel(DateTimeOffset at)
    {
        if (Status is not (ShipmentStatus.Pending or ShipmentStatus.Assigned))
        {
            return false;
        }

        Status = ShipmentStatus.Cancelled;
        CancelledAt = at;
        return true;
    }

    /// <summary>Order cancelled after dispatch: raises <see cref="ShipmentRecallRequired"/> while the box is out.</summary>
    public bool RequestRecall()
    {
        if (Status is not (ShipmentStatus.OutForDelivery or ShipmentStatus.Failed))
        {
            return false;
        }

        Raise(new ShipmentRecallRequired(Id, OrderId, FulfillmentLocationId, RiderId));
        return true;
    }

    /// <summary>From <c>OrderPacked</c> (A2). Already packed or cancelled → no change.</summary>
    public bool MarkPacked(DateTimeOffset packedAt, bool coldBoxUsed)
    {
        if (PackedAt is not null || Status == ShipmentStatus.Cancelled)
        {
            return false;
        }

        PackedAt = packedAt;
        ColdBoxUsed = coldBoxUsed;
        return true;
    }

    /// <summary>DEL-R5: PENDING / ASSIGNED / FAILED; rider active and at the shipment's location.</summary>
    public void AssignRider(Rider rider)
    {
        ArgumentNullException.ThrowIfNull(rider);
        if (Status is not (ShipmentStatus.Pending or ShipmentStatus.Assigned or ShipmentStatus.Failed))
        {
            throw InvalidTransition("assigned a rider");
        }

        if (!rider.IsActive || rider.FulfillmentLocationId != FulfillmentLocationId)
        {
            throw new DomainException(DeliveryErrors.RiderNotAvailable, "The rider is inactive or works at another location; choose another rider.", "riderId");
        }

        var previous = RiderId;
        RiderId = rider.Id;
        if (Status == ShipmentStatus.Pending)
        {
            Status = ShipmentStatus.Assigned;
        }

        Raise(new RiderAssigned(Id, OrderId, FulfillmentLocationId, rider.Id, previous));
    }

    /// <summary>DEL-R6, R14, R17: ASSIGNED + packed + active rider → OUT_FOR_DELIVERY with a new delivery code.</summary>
    public void Dispatch(Rider? rider, decimal dispatchTemperatureC, string deliveryCode, DateTimeOffset now)
    {
        if (Status != ShipmentStatus.Assigned || PackedAt is null || RiderId is null)
        {
            throw new ConflictException(DeliveryErrors.ShipmentNotDispatchable, "The shipment needs a packed order and an assigned rider before dispatch.");
        }

        if (rider is null || rider.Id != RiderId || !rider.IsActive)
        {
            throw new DomainException(DeliveryErrors.RiderNotAvailable, "The assigned rider is no longer active; assign another rider.", "riderId");
        }

        if (dispatchTemperatureC is < MinTemperatureC or > MaxTemperatureC || decimal.Round(dispatchTemperatureC, 1) != dispatchTemperatureC)
        {
            throw DeliveryErrors.Invalid("dispatchTemperatureC", "Dispatch temperature must be between -40.0 and 40.0 °C with one decimal.");
        }

        if (deliveryCode is not { Length: DeliveryCodeLength } || !deliveryCode.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("A delivery code is four digits.", nameof(deliveryCode));
        }

        Status = ShipmentStatus.OutForDelivery;
        DispatchedAt = now;
        DispatchTemperatureC = dispatchTemperatureC;
        TemperatureWarning = dispatchTemperatureC > MaxSafeTemperatureC;
        DeliveryCode = deliveryCode;
        DeliveryCodeFailures = 0;
        Raise(new ShipmentDispatched(Id, OrderId, FulfillmentLocationId, rider.Id, now, dispatchTemperatureC, TemperatureWarning, ColdBoxUsed));
    }

    /// <summary>
    /// DEL-R7: OUT_FOR_DELIVERY; locked after 5 wrong codes; a wrong code counts a failure and returns
    /// <see cref="DeliverOutcome.CodeInvalid"/> (the caller saves the counter, then refuses).
    /// </summary>
    public DeliverOutcome Deliver(string deliveryCode, CashReceipt? cash, DateTimeOffset now)
    {
        if (Status != ShipmentStatus.OutForDelivery)
        {
            throw InvalidTransition("delivered");
        }

        if (IsDeliveryCodeLocked)
        {
            throw new DomainException(DeliveryErrors.DeliveryCodeLocked, "Too many wrong delivery codes; record a failed attempt and reattempt the delivery.", "deliveryCode");
        }

        if (cash is not null)
        {
            ValidateCash(cash);
        }

        if (!CodeMatches(deliveryCode))
        {
            DeliveryCodeFailures++;
            return DeliverOutcome.CodeInvalid;
        }

        var riderId = RiderId ?? throw new InvalidOperationException("A shipment out for delivery has a rider (DEL-I4).");
        _attempts.Add(new DeliveryAttempt(Id, NextAttemptNo, AttemptOutcome.Delivered, null, now));
        Status = ShipmentStatus.Delivered;
        DeliveredAt = now;
        if (cash is not null)
        {
            CashCollectedPaise = cash.AmountPaise;
            CashMode = cash.Mode;
            UpiReference = cash.UpiReference;
            CashCollectedAt = now;
        }

        Raise(new ShipmentDelivered(Id, OrderId, FulfillmentLocationId, riderId, now, cash?.AmountPaise, cash?.Mode.ToCode(), cash?.UpiReference));
        return DeliverOutcome.Delivered;
    }

    /// <summary>§2 attempts: OUT_FOR_DELIVERY → FAILED with an attempt row.</summary>
    public void RecordFailedAttempt(AttemptOutcome outcome, string? notes, DateTimeOffset now)
    {
        if (Status != ShipmentStatus.OutForDelivery)
        {
            throw InvalidTransition("marked as a failed attempt");
        }

        if (outcome == AttemptOutcome.Delivered)
        {
            throw DeliveryErrors.Invalid("outcome", "A failed attempt needs a failure outcome; use deliver for a delivery.");
        }

        var trimmed = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (outcome == AttemptOutcome.Other && trimmed is null)
        {
            throw DeliveryErrors.Invalid("notes", "Notes are required when the outcome is OTHER.");
        }

        if (trimmed is { Length: > MaxNotesLength })
        {
            throw DeliveryErrors.Invalid("notes", "Notes must be at most 500 characters.");
        }

        var attemptNo = NextAttemptNo;
        _attempts.Add(new DeliveryAttempt(Id, attemptNo, outcome, trimmed, now));
        Status = ShipmentStatus.Failed;
        Raise(new DeliveryAttemptFailed(Id, OrderId, FulfillmentLocationId, attemptNo, outcome.ToCode(), trimmed, now));
    }

    /// <summary>
    /// DEL-R16: FAILED → OUT_FOR_DELIVERY while attempts so far are below <paramref name="maxAttempts"/> and today
    /// (local) is the slot's date; resets the code failure counter.
    /// </summary>
    public void Reattempt(int maxAttempts, DateOnly todayLocal, DateOnly slotDate)
    {
        if (Status != ShipmentStatus.Failed)
        {
            throw InvalidTransition("reattempted");
        }

        if (_attempts.Count >= maxAttempts || todayLocal != slotDate)
        {
            throw new DomainException(DeliveryErrors.ReattemptNotAllowed, "The delivery can no longer be reattempted; return the shipment.");
        }

        var riderId = RiderId ?? throw new InvalidOperationException("A failed shipment has a rider.");
        Status = ShipmentStatus.OutForDelivery;
        DeliveryCodeFailures = 0;
        Raise(new ShipmentReattempted(Id, OrderId, FulfillmentLocationId, riderId, NextAttemptNo));
    }

    /// <summary>FAILED → RETURNED (slot booking kept).</summary>
    public void Return(DateTimeOffset now)
    {
        if (Status != ShipmentStatus.Failed)
        {
            throw InvalidTransition("returned");
        }

        Status = ShipmentStatus.Returned;
        ReturnedAt = now;
        Raise(new ShipmentReturned(Id, OrderId, FulfillmentLocationId, now));
    }

    /// <summary>DEL-R14: four digits from a cryptographic RNG.</summary>
    public static string NewDeliveryCode()
        => RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);

    private bool CodeMatches(string? deliveryCode)
    {
        if (DeliveryCode is null || deliveryCode is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(DeliveryCode), Encoding.ASCII.GetBytes(deliveryCode.Trim()));
    }

    private void ValidateCash(CashReceipt cash)
    {
        if (CashCollectedPaise is not null)
        {
            throw new ConflictException(DeliveryErrors.CashAlreadyCollected, "Cash was already recorded for this shipment.", "cashCollectedPaise");
        }

        if (cash.AmountPaise < 0)
        {
            throw DeliveryErrors.Invalid("cashCollectedPaise", "Cash collected must not be negative.");
        }

        if (cash.Mode == Shipments.CashMode.Upi && string.IsNullOrWhiteSpace(cash.UpiReference))
        {
            throw DeliveryErrors.Invalid("upiReference", "A UPI collection needs the UPI reference.");
        }

        if (cash.UpiReference is { Length: > MaxUpiReferenceLength })
        {
            throw DeliveryErrors.Invalid("upiReference", "The UPI reference must be at most 100 characters.");
        }
    }

    private ConflictException InvalidTransition(string action)
        => new(DeliveryErrors.InvalidStatusTransition, $"A shipment in status {Status} cannot be {action}.");
}

/// <summary>One delivery attempt (immutable).</summary>
public sealed class DeliveryAttempt : Entity
{
    private DeliveryAttempt()
    {
    }

    internal DeliveryAttempt(Guid shipmentId, int attemptNo, AttemptOutcome outcome, string? notes, DateTimeOffset attemptedAt)
    {
        ShipmentId = shipmentId;
        AttemptNo = attemptNo;
        Outcome = outcome;
        Notes = notes;
        AttemptedAt = attemptedAt;
    }

    public Guid ShipmentId { get; private set; }

    public int AttemptNo { get; private set; }

    public AttemptOutcome Outcome { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }
}

public interface IShipmentRepository
{
    /// <summary>Shipment with its attempts (tracked).</summary>
    Task<Shipment?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Shipment?> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Shipments of the rider in ASSIGNED / OUT_FOR_DELIVERY / FAILED.</summary>
    Task<int> CountOpenForRiderAsync(Guid riderId, CancellationToken cancellationToken);

    void Add(Shipment shipment);
}
