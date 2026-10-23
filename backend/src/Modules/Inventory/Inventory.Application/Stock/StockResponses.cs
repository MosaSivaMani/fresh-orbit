using System.Globalization;
using FreshOrbit.Inventory.Domain.ColdChain;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Inventory.Application.Stock;

public sealed record QuantityDto(int Value, StockUnit Unit);

/// <param name="Version">Row version, also sent as ETag; quote it in If-Match.</param>
/// <param name="Batches">ACTIVE batches (detail only; null in lists).</param>
public sealed record StockItemResponse(
    Guid Id,
    string Version,
    Guid FulfillmentLocationId,
    Guid VariantId,
    StockUnit StockUnit,
    QuantityDto OnHand,
    QuantityDto Reserved,
    QuantityDto Available,
    int LowStockThreshold,
    bool IsLowStock,
    IReadOnlyList<BatchResponse>? Batches);

/// <param name="CostPerUnitPaise">Display only: round(total cost / received quantity) (INV-R16).</param>
/// <param name="StockItemVersion">Version of the owning stock item, to quote in If-Match for discard (API gap A5).</param>
public sealed record BatchResponse(
    Guid Id,
    Guid StockItemId,
    string? StockItemVersion,
    Guid FulfillmentLocationId,
    Guid VariantId,
    string BatchCode,
    BatchStatus Status,
    string? SupplierName,
    string? SourceLandingCenter,
    DateOnly? CaughtOrHarvestedOn,
    DateTimeOffset ReceivedAt,
    QuantityDto ReceivedQuantity,
    QuantityDto RemainingQuantity,
    DateTimeOffset BestBeforeAt,
    decimal? ReceiptTemperatureC,
    long TotalCostPaise,
    long CostPerUnitPaise);

public sealed record MovementResponse(
    Guid Id,
    Guid StockItemId,
    Guid? BatchId,
    MovementKind Kind,
    QuantityDto Quantity,
    long CostPaise,
    string? ReasonCode,
    string? Notes,
    MovementReferenceType ReferenceType,
    Guid? ReferenceId,
    DateTimeOffset OccurredAt,
    Guid? RecordedBy);

public sealed record ReceiptResponse(BatchResponse Batch, MovementResponse Movement, StockItemResponse StockItem, bool TemperatureWarning);

/// <summary>Result of waste, discard, adjustment and count: the movements written and the stock item after the change.</summary>
public sealed record StockChangeResponse(IReadOnlyList<MovementResponse> Movements, StockItemResponse StockItem);

public sealed record ReservationResponse(
    Guid Id,
    Guid StockItemId,
    Guid FulfillmentLocationId,
    Guid VariantId,
    Guid OrderId,
    Guid OrderItemId,
    QuantityDto Quantity,
    ReservationStatus Status,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);

public sealed record ColdChainLogResponse(
    Guid Id,
    Guid FulfillmentLocationId,
    string StorageUnit,
    decimal TemperatureC,
    bool IsOutOfRange,
    DateTimeOffset RecordedAt,
    Guid? RecordedBy);

public static class StockMappings
{
    public static string FormatVersion(uint version) => version.ToString(CultureInfo.InvariantCulture);

    public static QuantityDto Quantity(int value, StockUnit unit) => new(value, unit);

    public static StockItemResponse ToResponse(this StockItem item, bool includeBatches)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new StockItemResponse(
            item.Id,
            FormatVersion(item.Version),
            item.FulfillmentLocationId,
            item.VariantId,
            item.StockUnit,
            Quantity(item.OnHand, item.StockUnit),
            Quantity(item.Reserved, item.StockUnit),
            Quantity(Math.Max(0, item.Available), item.StockUnit),
            item.LowStockThreshold,
            item.LowStockThreshold > 0 && item.Available <= item.LowStockThreshold,
            includeBatches
                ? [.. item.Batches.Where(b => b.IsActive).OrderBy(b => b.BestBeforeAt).ThenBy(b => b.ReceivedAt).Select(b => b.ToResponse(item.StockUnit, FormatVersion(item.Version)))]
                : null);
    }

    public static BatchResponse ToResponse(this Batch batch, StockUnit unit, string? stockItemVersion)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return new BatchResponse(
            batch.Id,
            batch.StockItemId,
            stockItemVersion,
            batch.FulfillmentLocationId,
            batch.VariantId,
            batch.BatchCode,
            batch.Status,
            batch.SupplierName,
            batch.SourceLandingCenter,
            batch.CaughtOrHarvestedOn,
            batch.ReceivedAt,
            Quantity(batch.ReceivedQuantity, unit),
            Quantity(batch.RemainingQuantity, unit),
            batch.BestBeforeAt,
            batch.ReceiptTemperatureC,
            batch.TotalCostPaise,
            CostPerUnit(batch.TotalCostPaise, batch.ReceivedQuantity));
    }

    public static long CostPerUnit(long totalCostPaise, int receivedQuantity)
        => receivedQuantity == 0 ? 0 : Rounding.DivideHalfAwayFromZero(totalCostPaise, receivedQuantity);

    public static MovementResponse ToResponse(this StockMovement movement, StockUnit unit)
    {
        ArgumentNullException.ThrowIfNull(movement);
        return new MovementResponse(
            movement.Id,
            movement.StockItemId,
            movement.BatchId,
            movement.Kind,
            Quantity(movement.Quantity, unit),
            movement.CostPaise,
            movement.ReasonCode,
            movement.Notes,
            movement.ReferenceType,
            movement.ReferenceId,
            movement.OccurredAt,
            movement.RecordedBy);
    }

    public static StockChangeResponse ToChangeResponse(this StockItem item, IEnumerable<StockMovement> movements)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new StockChangeResponse([.. movements.Select(m => m.ToResponse(item.StockUnit))], item.ToResponse(includeBatches: true));
    }

    public static ColdChainLogResponse ToResponse(this ColdChainLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        return new ColdChainLogResponse(log.Id, log.FulfillmentLocationId, log.StorageUnit, log.TemperatureC, log.IsOutOfRange, log.RecordedAt, log.RecordedBy);
    }
}
