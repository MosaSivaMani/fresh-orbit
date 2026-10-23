using FreshOrbit.Inventory.Domain.Stock;

namespace FreshOrbit.Inventory.Application.Handlers;

/// <summary>Status of a line in <c>OrderPacked</c>. Stored/sent as ACTIVE / SHORT_SUPPLIED / SUBSTITUTED.</summary>
public enum PackedLineStatus
{
    Active,
    ShortSupplied,
    Substituted,
}

/// <param name="PackedQuantity">Net packed quantity in the variant's stock unit; its gross (INV-R11) leaves stock.</param>
public sealed record PackedLine(Guid OrderItemId, Guid VariantId, int YieldBp, int PackedQuantity, PackedLineStatus Status);

public sealed record PackedOrder(Guid OrderId, Guid FulfillmentLocationId, DateTimeOffset PackedAt, IReadOnlyList<PackedLine> Lines);

/// <summary>
/// Inventory's reactions to Ordering events (inventory.md §3 "Events consumed"). Each method only tracks changes;
/// the outbox dispatcher saves them together with the inbox row. All are idempotent by state, so a repeated
/// delivery (even without the inbox row) changes nothing. Stock items are loaded in one query per call.
/// </summary>
public sealed class OrderStockHandlers(IStockItemRepository stockItems)
{
    /// <summary><c>OrderConfirmed</c>: the order's ACTIVE reservations stop lapsing.</summary>
    public async Task ConfirmReservationsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        foreach (var item in await stockItems.GetForOrderAsync(orderId, cancellationToken))
        {
            if (item.ClearReservationExpiry(orderId) > 0)
            {
                stockItems.MarkChanged(item);
            }
        }
    }

    /// <summary><c>OrderPaymentFailed</c>: ACTIVE reservations → EXPIRED, reserved restored.</summary>
    public Task ExpireReservationsAsync(Guid orderId, CancellationToken cancellationToken)
        => CloseAsync(orderId, ReservationStatus.Expired, cancellationToken);

    /// <summary><c>OrderCancelled</c>: ACTIVE reservations → RELEASED (none left after packing: stock stays sold).</summary>
    public Task ReleaseReservationsAsync(Guid orderId, CancellationToken cancellationToken)
        => CloseAsync(orderId, ReservationStatus.Released, cancellationToken);

    /// <summary>
    /// <c>OrderPacked</c>: per line, ACTIVE line → reservation CONSUMED + SALE movements FEFO for the gross of the packed
    /// quantity; SHORT_SUPPLIED / SUBSTITUTED / packed 0 → reservation RELEASED. A line already consumed is skipped.
    /// </summary>
    public async Task ConsumeForPackedOrderAsync(PackedOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.Lines.Count == 0)
        {
            return;
        }

        var items = (await stockItems.GetManyWithActiveChildrenAsync(order.FulfillmentLocationId, [.. order.Lines.Select(l => l.VariantId).Distinct()], cancellationToken))
            .ToDictionary(i => i.VariantId);
        var sold = await stockItems.GetOrderItemsWithSaleAsync([.. order.Lines.Select(l => l.OrderItemId)], cancellationToken);
        foreach (var line in order.Lines)
        {
            var consume = line.Status == PackedLineStatus.Active && line.PackedQuantity > 0;
            if (!items.TryGetValue(line.VariantId, out var item))
            {
                if (consume)
                {
                    throw new InvalidOperationException($"No stock item exists for variant {line.VariantId} at location {order.FulfillmentLocationId}.");
                }

                continue;
            }

            var changed = consume
                ? item.ConsumeForPack(line.OrderItemId, StockQuantities.Gross(item.StockUnit, line.PackedQuantity, line.YieldBp), sold.Contains(line.OrderItemId), order.PackedAt)
                : item.ReleaseReservationOfItem(line.OrderItemId) > 0;
            if (changed)
            {
                stockItems.MarkChanged(item);
            }
        }
    }

    private async Task CloseAsync(Guid orderId, ReservationStatus status, CancellationToken cancellationToken)
    {
        foreach (var item in await stockItems.GetForOrderAsync(orderId, cancellationToken))
        {
            if (item.CloseReservationsOfOrder(orderId, status) > 0)
            {
                stockItems.MarkChanged(item);
            }
        }
    }
}
