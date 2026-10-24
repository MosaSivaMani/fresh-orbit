using FreshOrbit.Customers.Application.StockAlerts;
using FreshOrbit.Inventory.Public.Events;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Customers.Application.Handlers;

// Event adapters (D30): map another module's Public event onto this module's own service. They only track changes;
// the outbox dispatcher saves them with the inbox row (no SaveChanges here).

/// <summary>CUS-R9: stock is back at a location, so the alerts waiting for it fire and are cleared.</summary>
public sealed class CustomersStockAvailableAgainHandler(StockAlertTrigger trigger) : IEventHandler<StockAvailableAgain>
{
    public Task HandleAsync(StockAvailableAgain domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return trigger.TriggerAsync(domainEvent.VariantId, domainEvent.FulfillmentLocationId, cancellationToken);
    }
}
