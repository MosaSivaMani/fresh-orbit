namespace FreshOrbit.Inventory.Public;

/// <summary>What other modules may ask the Inventory module.</summary>
public interface IInventoryApi
{
    /// <summary>Available (on hand minus reserved) quantity per variant; variants without a stock item are absent.</summary>
    Task<IReadOnlyList<VariantAvailability>> GetAvailabilityAsync(Guid fulfillmentLocationId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserves stock for an order's lines, all or nothing. Changes are tracked on the shared DbContext and saved
    /// by the caller's single SaveChanges; this method never saves.
    /// </summary>
    Task<ReserveStockResult> ReserveAsync(ReserveStockRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Unit of an inventory quantity. Stored as GRAM / PIECE (same codes as Catalog's StockUnit; Public projects
/// reference only the SharedKernel, so the enum is declared here).
/// </summary>
public enum QuantityUnit
{
    Gram,
    Piece,
}

public sealed record VariantAvailability(Guid VariantId, int AvailableQuantity, QuantityUnit StockUnit);

/// <param name="ExpiresAt">When the reservation lapses if the order is not paid; null keeps it until consumed or released.</param>
public sealed record ReserveStockRequest(Guid OrderId, Guid FulfillmentLocationId, IReadOnlyList<ReserveStockLine> Lines, DateTimeOffset? ExpiresAt);

/// <param name="Quantity">Gross quantity in the variant's stock unit (grams after yield, or pieces).</param>
public sealed record ReserveStockLine(Guid OrderItemId, Guid VariantId, int Quantity);

public sealed record ReserveStockResult(bool Succeeded, IReadOnlyList<Guid> ShortVariantIds);
