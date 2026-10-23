using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Inventory.Application.Handlers;

// Event adapters (docs/team/integration.md): each maps another module's Public event onto this module's own handler
// service. They only track changes; the outbox dispatcher saves them with the inbox row (no SaveChanges here).

public sealed class InventoryOrderConfirmedHandler(OrderStockHandlers stock) : IEventHandler<OrderConfirmed>
{
    public Task HandleAsync(OrderConfirmed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return stock.ConfirmReservationsAsync(domainEvent.OrderId, cancellationToken);
    }
}

public sealed class InventoryOrderPaymentFailedHandler(OrderStockHandlers stock) : IEventHandler<OrderPaymentFailed>
{
    public Task HandleAsync(OrderPaymentFailed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return stock.ExpireReservationsAsync(domainEvent.OrderId, cancellationToken);
    }
}

public sealed class InventoryOrderCancelledHandler(OrderStockHandlers stock) : IEventHandler<OrderCancelled>
{
    public Task HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return stock.ReleaseReservationsAsync(domainEvent.OrderId, cancellationToken);
    }
}

public sealed class InventoryOrderPackedHandler(OrderStockHandlers stock) : IEventHandler<OrderPacked>
{
    public Task HandleAsync(OrderPacked domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return stock.ConsumeForPackedOrderAsync(
            new PackedOrder(
                domainEvent.OrderId,
                domainEvent.FulfillmentLocationId,
                domainEvent.PackedAt,
                [.. domainEvent.Lines.Select(l => new PackedLine(l.OrderItemId, l.VariantId, l.YieldBp, l.PackedQuantity, LineStatus(l.Status)))]),
            cancellationToken);
    }

    /// <summary>ACTIVE / SHORT_SUPPLIED / SUBSTITUTED; a CANCELLED line consumes nothing and is released like a short-supplied one.</summary>
    private static PackedLineStatus LineStatus(string code)
        => EnumCodes.TryToEnum<PackedLineStatus>(code, out var status) ? status : PackedLineStatus.ShortSupplied;
}
