using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.Promotions.Domain.Redemptions;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Promotions.Application.Handlers;

// Event adapters (docs/team/integration.md): each maps another module's Public event onto this module's own handler
// service. They only track changes; the outbox dispatcher saves them with the inbox row (no SaveChanges here).

public sealed class PromotionsOrderCancelledHandler(RedemptionReversalService reversals) : IEventHandler<OrderCancelled>
{
    public Task HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return reversals.ReverseForOrderAsync(domainEvent.OrderId, ReversalReason.OrderCancelled, cancellationToken);
    }
}

public sealed class PromotionsOrderPaymentFailedHandler(RedemptionReversalService reversals) : IEventHandler<OrderPaymentFailed>
{
    public Task HandleAsync(OrderPaymentFailed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return reversals.ReverseForOrderAsync(domainEvent.OrderId, ReversalReason.PaymentFailed, cancellationToken);
    }
}
