using System.Globalization;
using FreshOrbit.Inventory.Domain.ColdChain;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.SharedKernel.Units;
using VariantInfo = FreshOrbit.Catalog.Public.VariantInfo;

namespace FreshOrbit.Inventory.Application.Stock;

public sealed record QuantityDto(int Value, StockUnit Unit);

/// <param name="Version">Row version, also sent as ETag; quote it in If-Match.</param>
/// <param name="Batches">ACTIVE batches (detail only; null in lists).</param>
/// <param name="ProductName">Catalog names (G3): filled on the admin GET lists and detail; null in write responses.</param>
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
    IReadOnlyList<BatchResponse>? Batches,
    string? ProductName = null,
    string? VariantName = null,
    string? Sku = null);

/// <param name="CostPerUnitPaise">Display only: round(total cost / received quantity) (INV-R16).</param>
/// <param name="StockItemVersion">Version of the owning stock item, to quote in If-Match for discard (API gap A5).</param>
/// <param name="ProductName">Catalog names (G3): filled on the admin GET lists and detail; null in write responses.</param>
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
    long CostPerUnitPaise,
    string? ProductName = null,
    string? VariantName = null,
    string? Sku = null);

/// <param name="VariantId">The stock item's variant (G3): filled on <c>GET /movements</c>, like the names; null in write responses.</param>
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
    Guid? RecordedBy,
    Guid? VariantId = null,
    string? ProductName = null,
    string? VariantName = null,
    string? Sku = null);

public sealed record ReceiptResponse(BatchResponse Batch, MovementResponse Movement, StockItemResponse StockItem, bool TemperatureWarning);

/// <summary>Result of waste, discard, adjustment and count: the movements written and the stock item after the change.</summary>
public sealed record StockChangeResponse(IReadOnlyList<MovementResponse> Movements, StockItemResponse StockItem);

/// <param name="ProductName">Catalog names (G3).</param>
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
    DateTimeOffset CreatedAt,
    string? ProductName = null,
    string? VariantName = null,
    string? Sku = null);

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
    /// <summary>The stock item with Catalog names; unchanged when the variant is unknown to Catalog.</summary>
    public static StockItemResponse WithVariant(this StockItemResponse item, VariantInfo? variant)
    {
        ArgumentNullException.ThrowIfNull(item);
        return variant is null ? item : item with
        {
            ProductName = variant.ProductName,
            VariantName = variant.VariantName,
            Sku = variant.Sku,
            Batches = item.Batches is null ? null : [.. item.Batches.Select(b => b.WithVariant(variant))],
        };
    }

    public static BatchResponse WithVariant(this BatchResponse batch, VariantInfo? variant)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return variant is null ? batch : batch with { ProductName = variant.ProductName, VariantName = variant.VariantName, Sku = variant.Sku };
    }

    public static MovementResponse WithVariant(this MovementResponse movement, Guid variantId, VariantInfo? variant)
    {
        ArgumentNullException.ThrowIfNull(movement);
        return movement with { VariantId = variantId, ProductName = variant?.ProductName, VariantName = variant?.VariantName, Sku = variant?.Sku };
    }

    public static ReservationResponse WithVariant(this ReservationResponse reservation, VariantInfo? variant)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return variant is null ? reservation : reservation with { ProductName = variant.ProductName, VariantName = variant.VariantName, Sku = variant.Sku };
    }

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
