using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Inventory.Domain.Stock;

/// <summary>An insert-only ledger row: a signed quantity and its cost (same sign) for a stock item and, usually, a batch.</summary>
public sealed class StockMovement : Entity
{
    private StockMovement()
    {
    }

    internal StockMovement(
        Guid stockItemId,
        Guid? batchId,
        MovementKind kind,
        int quantity,
        long costPaise,
        string? reasonCode,
        string? notes,
        MovementReferenceType referenceType,
        Guid? referenceId,
        DateTimeOffset occurredAt,
        Guid? recordedBy)
    {
        StockItemId = stockItemId;
        BatchId = batchId;
        Kind = kind;
        Quantity = quantity;
        CostPaise = costPaise;
        ReasonCode = reasonCode;
        Notes = notes;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        OccurredAt = occurredAt;
        RecordedBy = recordedBy;
    }

    public Guid StockItemId { get; private set; }

    /// <summary>Null only for the batchless pack-shortfall pair (INV-R14).</summary>
    public Guid? BatchId { get; private set; }

    public MovementKind Kind { get; private set; }

    public int Quantity { get; private set; }

    public long CostPaise { get; private set; }

    public string? ReasonCode { get; private set; }

    public string? Notes { get; private set; }

    public MovementReferenceType ReferenceType { get; private set; }

    public Guid? ReferenceId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Staff member who recorded it; null for the system.</summary>
    public Guid? RecordedBy { get; private set; }
}
