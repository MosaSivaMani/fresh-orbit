namespace FreshOrbit.Locations.Public;

/// <summary>
/// Implemented by modules whose configuration a city needs before it can be activated (Pricing, Inventory,
/// Delivery, …). Locations calls every registered implementation and refuses activation while any reports items.
/// </summary>
public interface ICityActivationCheck
{
    /// <summary>UPPER_SNAKE codes of what is still missing for the city (e.g. NO_ACTIVE_PRICE); empty when ready.</summary>
    Task<IReadOnlyList<string>> GetMissingItemsAsync(Guid cityId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implemented by modules that can block retiring a city (e.g. Ordering with open orders, Delivery with booked slots).
/// </summary>
public interface ICityRetirementCheck
{
    /// <summary>UPPER_SNAKE codes of what still blocks retirement (e.g. OPEN_ORDERS_EXIST); empty when it may retire.</summary>
    Task<IReadOnlyList<string>> GetBlockingItemsAsync(Guid cityId, CancellationToken cancellationToken = default);
}

/// <summary>Implemented by Delivery: the next slot a customer could still book at a fulfillment location.</summary>
public interface INextSlotProvider
{
    /// <summary>The earliest open slot whose cutoff has not passed and that has capacity left, or null.</summary>
    Task<NextSlot?> GetNextOpenSlotAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken = default);
}

public sealed record NextSlot(Guid SlotId, DateTimeOffset StartAt, DateTimeOffset EndAt, DateTimeOffset CutoffAt);
