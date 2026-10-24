using FreshOrbit.Customers.Public.Events;
using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Customers.Domain.StockAlerts;

/// <summary>
/// "Notify me when back in stock" (CUS-R9): one per customer, variant and fulfillment location. Removed when it fires
/// (the stock came back) or when the customer removes it; never edited.
/// </summary>
public sealed class StockAlert : AggregateRoot
{
    /// <summary>Most alerts one customer may hold at a time (CUS-R9).</summary>
    public const int MaxPerCustomer = 50;

    private StockAlert()
    {
    }

    public Guid CustomerId { get; private set; }

    public Guid VariantId { get; private set; }

    public Guid FulfillmentLocationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static StockAlert Create(Guid customerId, Guid variantId, Guid fulfillmentLocationId, DateTimeOffset now)
        => new() { CustomerId = customerId, VariantId = variantId, FulfillmentLocationId = fulfillmentLocationId, CreatedAt = now };

    /// <summary>The stock is back: raises <see cref="StockAlertTriggered"/>; the caller removes the alert in the same save.</summary>
    public void Trigger(string productName, string productSlug, string variantName)
        => Raise(new StockAlertTriggered(Id, CustomerId, VariantId, FulfillmentLocationId, productName, productSlug, variantName));
}
