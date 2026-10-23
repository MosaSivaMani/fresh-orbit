using FreshOrbit.Delivery.Public.Events;
using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Payments.Application.Handlers;

// Event adapters (docs/team/integration.md): each maps another module's Public event onto this module's own handler
// service. They only track changes; the outbox dispatcher saves them with the inbox row (no SaveChanges here).

public sealed class PaymentsOrderPlacedHandler(OrderPaymentHandlers handlers) : IEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return handlers.OnOrderPlacedAsync(new OrderPlacedInput(domainEvent.OrderId, domainEvent.CustomerId, domainEvent.FulfillmentLocationId, domainEvent.PaymentMethod, domainEvent.AmountDuePaise), cancellationToken);
    }
}

public sealed class PaymentsOrderPackedHandler(OrderPaymentHandlers handlers) : IEventHandler<OrderPacked>
{
    public Task HandleAsync(OrderPacked domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return handlers.OnOrderPackedAsync(
            new OrderPackedInput(
                domainEvent.OrderId,
                domainEvent.PaymentMethod,
                domainEvent.AmountDuePaise,
                domainEvent.RefundDuePaise,
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    ["WEIGHT_SHORTFALL"] = domainEvent.RefundDueByReason.WeightShortfall,
                    ["SHORT_SUPPLY"] = domainEvent.RefundDueByReason.ShortSupply,
                }),
            cancellationToken);
    }
}

public sealed class PaymentsOrderAdjustedHandler(OrderPaymentHandlers handlers) : IEventHandler<OrderAdjusted>
{
    public Task HandleAsync(OrderAdjusted domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return handlers.OnOrderAdjustedAsync(new OrderAdjustedInput(domainEvent.OrderId, domainEvent.AdjustmentId, domainEvent.AmountDuePaise, domainEvent.RefundDuePaise, domainEvent.RequestedBy), cancellationToken);
    }
}

public sealed class PaymentsOrderCancelledHandler(OrderPaymentHandlers handlers) : IEventHandler<OrderCancelled>
{
    public Task HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return handlers.OnOrderCancelledAsync(domainEvent.OrderId, cancellationToken);
    }
}

public sealed class PaymentsOrderPaymentFailedHandler(OrderPaymentHandlers handlers) : IEventHandler<OrderPaymentFailed>
{
    public Task HandleAsync(OrderPaymentFailed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return handlers.OnOrderPaymentFailedAsync(domainEvent.OrderId, cancellationToken);
    }
}

public sealed class PaymentsOrderReturnedHandler(OrderPaymentHandlers handlers) : IEventHandler<OrderReturned>
{
    public Task HandleAsync(OrderReturned domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return handlers.OnOrderReturnedAsync(domainEvent.OrderId, cancellationToken);
    }
}

public sealed class PaymentsShipmentDeliveredHandler(OrderPaymentHandlers handlers) : IEventHandler<ShipmentDelivered>
{
    public Task HandleAsync(ShipmentDelivered domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return handlers.OnShipmentDeliveredAsync(
            new ShipmentDeliveredInput(domainEvent.OrderId, domainEvent.ShipmentId, domainEvent.RiderId, domainEvent.DeliveredAt, domainEvent.CashCollectedPaise, domainEvent.CashMode, domainEvent.UpiReference),
            cancellationToken);
    }
}

public sealed class PaymentsReceivedForClosedOrderHandler(OrderPaymentHandlers handlers) : IEventHandler<PaymentReceivedForClosedOrder>
{
    public Task HandleAsync(PaymentReceivedForClosedOrder domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return handlers.OnPaymentReceivedForClosedOrderAsync(domainEvent.OrderId, domainEvent.PaymentId, cancellationToken);
    }
}

public sealed class PaymentsComplaintDecidedHandler(OrderPaymentHandlers handlers) : IEventHandler<ComplaintDecided>
{
    public Task HandleAsync(ComplaintDecided domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return domainEvent.DecidedBy is { } decidedBy
            ? handlers.OnComplaintDecidedAsync(new ComplaintDecidedInput(domainEvent.OrderId, domainEvent.ComplaintId, domainEvent.Resolution, domainEvent.RefundAmountPaise, domainEvent.RefundMethod, decidedBy), cancellationToken)
            : Task.CompletedTask;
    }
}
