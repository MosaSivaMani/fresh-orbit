using FreshOrbit.Payments.Public.Events;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Payments.Domain.Settlements;

/// <summary>Stored as OPEN / SETTLED / DISPUTED.</summary>
public enum CashSettlementStatus
{
    Open,
    Settled,
    Disputed,
}

/// <summary>A rider's cash handed over for one settlement date (PAY-R19, R20).</summary>
public sealed class CashSettlement : AggregateRoot
{
    private CashSettlement()
    {
    }

    private CashSettlement(Guid riderId, Guid fulfillmentLocationId, DateOnly settlementDate, long expectedPaise, long receivedPaise, int collectionsCount, Guid? recordedBy, string? note)
    {
        RiderId = riderId;
        FulfillmentLocationId = fulfillmentLocationId;
        SettlementDate = settlementDate;
        ExpectedPaise = expectedPaise;
        ReceivedPaise = receivedPaise;
        DifferencePaise = receivedPaise - expectedPaise;
        CollectionsCount = collectionsCount;
        RecordedBy = recordedBy;
        Note = note;
        Status = DifferencePaise == 0 ? CashSettlementStatus.Settled : CashSettlementStatus.Open;
        SettledBy = Status == CashSettlementStatus.Settled ? recordedBy : null;
    }

    public Guid RiderId { get; private set; }

    public Guid FulfillmentLocationId { get; private set; }

    public DateOnly SettlementDate { get; private set; }

    public long ExpectedPaise { get; private set; }

    public long ReceivedPaise { get; private set; }

    /// <summary>received − expected.</summary>
    public long DifferencePaise { get; private set; }

    public CashSettlementStatus Status { get; private set; }

    public int CollectionsCount { get; private set; }

    /// <summary>Staff user who recorded the settlement (the creator for the resolve rule).</summary>
    public Guid? RecordedBy { get; private set; }

    public Guid? SettledBy { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    public string? Note { get; private set; }

    public string? DisputeReason { get; private set; }

    public static CashSettlement Create(Guid riderId, Guid fulfillmentLocationId, DateOnly settlementDate, long expectedPaise, long receivedPaise, int collectionsCount, Guid? recordedBy, string? note, DateTimeOffset now)
    {
        if (receivedPaise < 0)
        {
            throw new RefusalException(new FieldError("receivedPaise", PaymentsErrors.AmountInvalid, "Received paise must not be negative."));
        }

        var settlement = new CashSettlement(riderId, fulfillmentLocationId, settlementDate, expectedPaise, receivedPaise, collectionsCount, recordedBy, note);
        if (settlement.Status == CashSettlementStatus.Settled)
        {
            settlement.SettledAt = now;
        }

        settlement.RaiseRecorded();
        return settlement;
    }

    /// <summary>OPEN/SETTLED → DISPUTED.</summary>
    public void Dispute(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new RefusalException(new FieldError("reason", PaymentsErrors.ReasonRequired, "Reason is required to dispute a settlement."));
        }

        if (Status is not (CashSettlementStatus.Open or CashSettlementStatus.Settled))
        {
            throw new ConflictException(PaymentsErrors.InvalidStatusTransition, "Only an OPEN or SETTLED settlement can be disputed.");
        }

        Status = CashSettlementStatus.Disputed;
        DisputeReason = reason.Trim();
        RaiseRecorded();
    }

    /// <summary>OPEN/DISPUTED → SETTLED with a note; a settlement with a difference needs a resolver other than its creator.</summary>
    public void Settle(string note, Guid resolvedBy, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new RefusalException(new FieldError("note", PaymentsErrors.ReasonRequired, "Note is required to settle."));
        }

        if (Status is not (CashSettlementStatus.Open or CashSettlementStatus.Disputed))
        {
            throw new ConflictException(PaymentsErrors.InvalidStatusTransition, "Only an OPEN or DISPUTED settlement can be settled.");
        }

        if (DifferencePaise != 0 && RecordedBy == resolvedBy)
        {
            throw new DomainException(PaymentsErrors.SettlementSelfResolution, "A settlement with a difference must be settled by someone other than its creator.");
        }

        Status = CashSettlementStatus.Settled;
        SettledBy = resolvedBy;
        SettledAt = now;
        Note = note.Trim();
        RaiseRecorded();
    }

    private void RaiseRecorded() => Raise(new CashSettlementRecorded(Id, RiderId, SettlementDate, DifferencePaise, Status.ToCode()));
}
