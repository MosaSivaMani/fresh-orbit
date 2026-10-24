using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Inventory.Public.Events;

/// <summary>
/// Available stock of a variant at a location went from zero (or below) to above zero, in the save that changed it
/// (receipt, adjustment, count, released reservation). Consumed by Customers for back-in-stock alerts.
/// </summary>
/// <param name="Available">Available quantity after the change, in the stock unit (grams or pieces).</param>
public sealed record StockAvailableAgain(Guid StockItemId, Guid FulfillmentLocationId, Guid VariantId, int Available) : DomainEvent;
