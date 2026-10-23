using FreshOrbit.Cart.Public;
using FreshOrbit.Identity.Public.Events;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Cart.Application.Handlers;

// Event adapters (docs/team/integration.md): each maps another module's Public event onto this module's own handler
// service. They only track changes; the outbox dispatcher saves them with the inbox row (no SaveChanges here).

public sealed class CartCustomerSignedInHandler(ICartApi carts) : IEventHandler<CustomerSignedIn>
{
    public Task HandleAsync(CustomerSignedIn domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return string.IsNullOrWhiteSpace(domainEvent.GuestCartToken)
            ? Task.CompletedTask
            : carts.MergeGuestCartAsync(domainEvent.GuestCartToken, domainEvent.CustomerId, cancellationToken);
    }
}
