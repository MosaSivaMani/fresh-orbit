using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Inventory.Public;

/// <summary>What other modules may ask the Inventory module.</summary>
public interface IInventoryApi
{
    /// <summary>Available (on hand minus reserved, never below 0) quantity per variant; variants without a stock item are absent.</summary>
    Task<IReadOnlyList<VariantAvailability>> GetAvailabilityAsync(Guid fulfillmentLocationId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserves stock for an order's lines, all or nothing. Changes are tracked on the shared DbContext and saved
    /// by the caller's single SaveChanges; this method never saves. A refusal changes nothing (OUT_OF_STOCK,
    /// STOCK_ITEM_MISSING per line in <see cref="ReserveStockResult.Refusals"/>). A lost race at the caller's save
    /// throws <c>ConcurrentSaveException</c>; the caller retries the whole command from Read.
    /// </summary>
    Task<ReserveStockResult> ReserveAsync(ReserveStockRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the ACTIVE reservations of the given order items (substitution accept). Tracked only; the caller saves.
    /// Items without an ACTIVE reservation are ignored.
    /// </summary>
    Task ReleaseAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken cancellationToken = default);

    /// <summary>Reservations of an order and the SALE movements of its items (admin order detail).</summary>
    Task<OrderStock> GetOrderStockAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Whether the location has at least one stock item (readiness).</summary>
    Task<bool> HasStockItemAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken = default);
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

/// <summary>Gross raw quantity for a net (prepared) quantity (INV-R11). Cart and Ordering use this, never their own formula.</summary>
public static class GrossQuantity
{
    /// <summary>GRAM: ceil(net × 10 000 / yield_bp) (500 g at 6 500 bp → 770 g); PIECE: net, yield ignored.</summary>
    public static int From(QuantityUnit stockUnit, int netQuantity, int yieldBp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(netQuantity);
        return stockUnit == QuantityUnit.Piece ? netQuantity : Weight.GrossForNet(netQuantity, yieldBp);
    }

    /// <summary>Largest net quantity whose gross fits in <paramref name="available"/> (INV-R12); the caller rounds down to the order step.</summary>
    public static int MaxNet(QuantityUnit stockUnit, int available, int yieldBp)
    {
        if (available <= 0)
        {
            return 0;
        }

        if (stockUnit == QuantityUnit.Piece)
        {
            return available;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(yieldBp, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(yieldBp, BasisPoints.Whole);
        return (int)((long)available * yieldBp / BasisPoints.Whole);
    }
}

public sealed record VariantAvailability(Guid VariantId, int AvailableQuantity, QuantityUnit StockUnit)
{
    /// <summary>Low-stock threshold in the stock unit; 0 = none (INV-R13 LOW_STOCK when available ≤ threshold).</summary>
    public int LowStockThreshold { get; init; }
}

/// <param name="ExpiresAt">When the reservation lapses if the order is not paid; null keeps it until consumed or released.</param>
public sealed record ReserveStockRequest(Guid OrderId, Guid FulfillmentLocationId, IReadOnlyList<ReserveStockLine> Lines, DateTimeOffset? ExpiresAt);

/// <param name="Quantity">Gross quantity in the variant's stock unit (grams after yield, or pieces); see <see cref="GrossQuantity"/>.</param>
public sealed record ReserveStockLine(Guid OrderItemId, Guid VariantId, int Quantity);

public sealed record ReserveStockResult(bool Succeeded, IReadOnlyList<Guid> ShortVariantIds)
{
    /// <summary>One entry per refused line (empty on success).</summary>
    public IReadOnlyList<ReserveStockRefusal> Refusals { get; init; } = [];
}

/// <param name="Code">OUT_OF_STOCK or STOCK_ITEM_MISSING.</param>
public sealed record ReserveStockRefusal(Guid OrderItemId, Guid VariantId, string Code, int RequestedQuantity, int AvailableQuantity);

public sealed record OrderStock(Guid OrderId, IReadOnlyList<OrderStockReservation> Reservations, IReadOnlyList<OrderStockSale> Sales);

/// <param name="Status">ACTIVE, CONSUMED, RELEASED or EXPIRED.</param>
public sealed record OrderStockReservation(Guid ReservationId, Guid OrderItemId, Guid VariantId, int Quantity, QuantityUnit StockUnit, string Status, DateTimeOffset? ExpiresAt);

/// <param name="BatchId">Null for the batchless pack-shortfall movement.</param>
public sealed record OrderStockSale(Guid MovementId, Guid OrderItemId, Guid VariantId, Guid? BatchId, string? BatchCode, int Quantity, long CostPaise, DateTimeOffset OccurredAt);
