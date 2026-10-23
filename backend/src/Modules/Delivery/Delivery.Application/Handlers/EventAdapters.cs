using FreshOrbit.Locations.Public.Events;
using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Delivery.Application.Handlers;

// Event adapters (docs/team/integration.md): each maps another module's Public event onto this module's own handler
// service. They only track changes; the outbox dispatcher saves them with the inbox row (no SaveChanges here).

public sealed class DeliveryOrderPlacedHandler(OrderEventHandlers orders) : IEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return orders.OnOrderPlacedAsync(domainEvent.OrderId, domainEvent.FulfillmentLocationId, domainEvent.DeliverySlotId, domainEvent.PlacedAt, cancellationToken);
    }
}

public sealed class DeliveryOrderCancelledHandler(OrderEventHandlers orders) : IEventHandler<OrderCancelled>
{
    public Task HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return orders.OnOrderCancelledAsync(domainEvent.OrderId, domainEvent.FulfillmentLocationId, domainEvent.DeliverySlotId, domainEvent.CancelledAt, cancellationToken);
    }
}

public sealed class DeliveryOrderPaymentFailedHandler(OrderEventHandlers orders) : IEventHandler<OrderPaymentFailed>
{
    public Task HandleAsync(OrderPaymentFailed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return orders.OnOrderPaymentFailedAsync(domainEvent.OrderId, domainEvent.FulfillmentLocationId, domainEvent.DeliverySlotId, domainEvent.At, cancellationToken);
    }
}

public sealed class DeliveryOrderPackedHandler(OrderEventHandlers orders) : IEventHandler<OrderPacked>
{
    public Task HandleAsync(OrderPacked domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return orders.OnOrderPackedAsync(domainEvent.OrderId, domainEvent.PackedAt, domainEvent.ColdBoxUsed, cancellationToken);
    }
}

public sealed class DeliveryClosureAddedHandler(LocationEventHandlers locations) : IEventHandler<LocationClosureAdded>
{
    public Task HandleAsync(LocationClosureAdded domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return locations.OnClosureAddedAsync(domainEvent.FulfillmentLocationId, domainEvent.ClosedOn, cancellationToken);
    }
}

public sealed class DeliveryClosureRemovedHandler(LocationEventHandlers locations) : IEventHandler<LocationClosureRemoved>
{
    public Task HandleAsync(LocationClosureRemoved domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return locations.OnClosureRemovedAsync(domainEvent.FulfillmentLocationId, domainEvent.ClosedOn, cancellationToken);
    }
}

public sealed class DeliveryLocationStatusChangedHandler(LocationEventHandlers locations) : IEventHandler<FulfillmentLocationStatusChanged>
{
    private const string ActiveStatus = "ACTIVE";

    public Task HandleAsync(FulfillmentLocationStatusChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return string.Equals(domainEvent.To, ActiveStatus, StringComparison.Ordinal)
            ? locations.OnLocationActivatedAsync(domainEvent.FulfillmentLocationId, cancellationToken)
            : Task.CompletedTask;
    }
}
