using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Inventory.Domain.Stock.Events;

/// <summary>A batch was received (RECEIPT movement).</summary>
public sealed record StockReceived(
    Guid StockItemId,
    Guid FulfillmentLocationId,
    Guid VariantId,
    Guid BatchId,
    string BatchCode,
    int Quantity,
    long CostPaise,
    DateTimeOffset BestBeforeAt,
    bool TemperatureWarning) : DomainEvent;

/// <summary>One batch's part of a waste record.</summary>
public sealed record WasteLine(Guid BatchId, int Quantity, long CostPaise);

/// <summary>Waste was recorded (manual waste or batch discard). Quantities and costs are negative movements.</summary>
public sealed record WasteRecorded(
    Guid StockItemId,
    Guid FulfillmentLocationId,
    Guid VariantId,
    string ReasonCode,
    IReadOnlyList<WasteLine> Lines,
    int TotalQuantity,
    long TotalCostPaise) : DomainEvent;

/// <summary>The expiry job wrote off a batch past its best-before time.</summary>
public sealed record BatchExpired(
    Guid BatchId,
    Guid StockItemId,
    Guid FulfillmentLocationId,
    Guid VariantId,
    int WrittenOffQuantity,
    long CostPaise) : DomainEvent;

/// <summary>An adjustment or a count changed on hand. <paramref name="Kind"/> is the movement kind code.</summary>
public sealed record StockAdjusted(
    Guid StockItemId,
    Guid FulfillmentLocationId,
    Guid VariantId,
    string Kind,
    string? ReasonCode,
    int Quantity,
    long CostPaise) : DomainEvent;

/// <summary>Available fell to or below the low-stock threshold (INV-R17).</summary>
public sealed record LowStockReached(Guid StockItemId, Guid FulfillmentLocationId, Guid VariantId, int Available, int Threshold) : DomainEvent;

/// <summary>Available fell to zero or below (INV-R17).</summary>
public sealed record OutOfStockReached(Guid StockItemId, Guid FulfillmentLocationId, Guid VariantId, int Available, int Threshold) : DomainEvent;

/// <summary>Available rose above the threshold again (INV-R17).</summary>
public sealed record StockReplenished(Guid StockItemId, Guid FulfillmentLocationId, Guid VariantId, int Available, int Threshold) : DomainEvent;

/// <summary>A physical fact left less stock than is reserved (D7); staff should short-supply or substitute.</summary>
public sealed record StockOverCommitted(Guid StockItemId, Guid FulfillmentLocationId, Guid VariantId, int OnHand, int Reserved, int Shortfall) : DomainEvent;

/// <summary>Packing took more than the batches held; a batchless correction pair was written (INV-R14).</summary>
public sealed record StockDiscrepancyFound(Guid StockItemId, Guid FulfillmentLocationId, Guid VariantId, Guid OrderItemId, int GapQuantity) : DomainEvent;

/// <summary>One location's items at or below their threshold (low-stock digest job).</summary>
public sealed record LowStockDigestItem(Guid StockItemId, Guid VariantId, int Available, int Threshold);

/// <summary>Morning digest of low stock for one location.</summary>
public sealed record LowStockDigestReady(Guid FulfillmentLocationId, IReadOnlyList<LowStockDigestItem> Items) : DomainEvent;
