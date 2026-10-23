namespace FreshOrbit.Locations.Public;

/// <summary>
/// Implemented by modules whose configuration a city needs before it can be activated (Pricing, Inventory,
/// Delivery, …). Locations calls every registered implementation once per readiness check and refuses activation
/// while any reports items (LOC-R6); the refusal lists every item of every check.
/// </summary>
public interface ICityActivationCheck
{
    /// <summary>UPPER_SNAKE codes of what is still missing for the city (e.g. STOCK_ITEM_MISSING); empty when ready.</summary>
    Task<IReadOnlyList<string>> GetMissingItemsAsync(Guid cityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// What is missing, per fulfillment location where it applies. <paramref name="activeLocationIds"/> are the
    /// city's ACTIVE locations (the ones readiness is checked for). The default wraps
    /// <see cref="GetMissingItemsAsync"/> without location ids; implement it to report location ids.
    /// </summary>
    async Task<IReadOnlyList<CityMissingItem>> GetMissingDetailsAsync(Guid cityId, IReadOnlyCollection<Guid> activeLocationIds, CancellationToken cancellationToken = default)
        => [.. (await GetMissingItemsAsync(cityId, cancellationToken)).Select(code => new CityMissingItem(code, null, $"{code} reported by readiness check."))];
}

/// <summary>One missing piece of configuration (<c>missing[]</c> of <c>CITY_NOT_READY</c>).</summary>
/// <param name="Code">UPPER_SNAKE, e.g. PRICE_MISSING.</param>
/// <param name="FulfillmentLocationId">The location it applies to; null for the city as a whole.</param>
public sealed record CityMissingItem(string Code, Guid? FulfillmentLocationId, string Message);

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
