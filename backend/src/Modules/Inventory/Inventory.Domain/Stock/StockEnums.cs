namespace FreshOrbit.Inventory.Domain.Stock;

/// <summary>Unit of every quantity of a stock item. Stored as GRAM / PIECE (PACK variants use PIECE, D10).</summary>
public enum StockUnit
{
    Gram,
    Piece,
}

/// <summary>Stored as ACTIVE / DEPLETED / EXPIRED / DISCARDED.</summary>
public enum BatchStatus
{
    Active,
    Depleted,
    Expired,
    Discarded,
}

/// <summary>Stored as RECEIPT / SALE / WASTE / ADJUSTMENT_IN / ADJUSTMENT_OUT / RETURN / COUNT_CORRECTION.</summary>
public enum MovementKind
{
    Receipt,
    Sale,
    Waste,
    AdjustmentIn,
    AdjustmentOut,
    Return,
    CountCorrection,
}

/// <summary>What a movement refers to (INV-R21). Stored as ORDER_ITEM / BATCH / MANUAL.</summary>
public enum MovementReferenceType
{
    OrderItem,
    Batch,
    Manual,
}

/// <summary>Stored as ACTIVE / CONSUMED / RELEASED / EXPIRED.</summary>
public enum ReservationStatus
{
    Active,
    Consumed,
    Released,
    Expired,
}

/// <summary>Waste reasons (INV-R5). Stored as SPOILED / TEMPERATURE_BREACH / DAMAGED / EXPIRED / QC_REJECTED.</summary>
public enum WasteReason
{
    Spoiled,
    TemperatureBreach,
    Damaged,
    Expired,
    QcRejected,
}

/// <summary>Adjustment reasons (INV-R5, D11). <see cref="Other"/> needs notes.</summary>
public enum AdjustmentReason
{
    DataEntryError,
    Found,
    Lost,
    Theft,
    Transfer,
    Other,
}

/// <summary>Kinds an adjustment request may name (RETURN needs an explicit batch, API gap A3).</summary>
public enum AdjustmentKind
{
    AdjustmentIn,
    AdjustmentOut,
    Return,
}

/// <summary>Reason codes written by the system.</summary>
public static class SystemReasonCodes
{
    /// <summary>Batchless pair written when packing takes more than the batches hold (INV-R14).</summary>
    public const string PackShortfall = "PACK_SHORTFALL";

    public const string Expired = "EXPIRED";
}
