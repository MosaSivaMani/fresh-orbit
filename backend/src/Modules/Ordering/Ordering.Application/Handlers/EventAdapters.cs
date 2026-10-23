using FreshOrbit.Delivery.Public.Events;
using FreshOrbit.Ordering.Application.Events;
using FreshOrbit.Payments.Public.Events;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Ordering.Application.Handlers;

// Event adapters (docs/team/integration.md): each maps another module's Public event onto this module's own handler
// service. They only track changes; the outbox dispatcher saves them with the inbox row (no SaveChanges here).

public sealed class OrderingPaymentCapturedHandler(OrderEventService orders) : IEventHandler<PaymentCaptured>
{
    public Task HandleAsync(PaymentCaptured domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return orders.OnPaymentCapturedAsync(domainEvent.OrderId, domainEvent.PaymentId, domainEvent.AmountPaise, domainEvent.CapturedAt, cancellationToken);
    }
}

public sealed class OrderingPaymentFailedHandler(OrderEventService orders) : IEventHandler<PaymentFailed>
{
    public Task HandleAsync(PaymentFailed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return orders.OnPaymentFailedAsync(domainEvent.OrderId, domainEvent.PaymentId, domainEvent.FailureCode, domainEvent.HasOtherActiveAttempt, cancellationToken);
    }
}

public sealed class OrderingShipmentDispatchedHandler(OrderEventService orders) : IEventHandler<ShipmentDispatched>
{
    public Task HandleAsync(ShipmentDispatched domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return orders.OnShipmentDispatchedAsync(domainEvent.OrderId, domainEvent.ShipmentId, domainEvent.RiderId, domainEvent.DispatchedAt, cancellationToken);
    }
}

public sealed class OrderingShipmentDeliveredHandler(OrderEventService orders) : IEventHandler<ShipmentDelivered>
{
    public Task HandleAsync(ShipmentDelivered domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return orders.OnShipmentDeliveredAsync(domainEvent.OrderId, domainEvent.ShipmentId, domainEvent.DeliveredAt, cancellationToken);
    }
}

public sealed class OrderingDeliveryAttemptFailedHandler(OrderEventService orders) : IEventHandler<DeliveryAttemptFailed>
{
    public Task HandleAsync(DeliveryAttemptFailed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return orders.OnDeliveryAttemptFailedAsync(domainEvent.OrderId, domainEvent.AttemptNo, domainEvent.Outcome, domainEvent.AttemptedAt, cancellationToken);
    }
}

public sealed class OrderingShipmentReattemptedHandler(OrderEventService orders) : IEventHandler<ShipmentReattempted>
{
    public Task HandleAsync(ShipmentReattempted domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return orders.OnShipmentReattemptedAsync(domainEvent.OrderId, cancellationToken);
    }
}

public sealed class OrderingShipmentReturnedHandler(OrderEventService orders) : IEventHandler<ShipmentReturned>
{
    public Task HandleAsync(ShipmentReturned domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return orders.OnShipmentReturnedAsync(domainEvent.OrderId, domainEvent.ShipmentId, domainEvent.ReturnedAt, cancellationToken);
    }
}
