namespace FreshOrbit.Pricing.Public;

/// <summary>What other modules may ask the Pricing module.</summary>
public interface IPricingApi
{
    /// <summary>
    /// Prices valid at <paramref name="at"/> at the location. A key without a price is absent from the result:
    /// it is not for sale there.
    /// </summary>
    Task<IReadOnlyList<LinePrice>> GetLinePricesAsync(Guid fulfillmentLocationId, IReadOnlyCollection<PriceKey> keys, DateTimeOffset at, CancellationToken cancellationToken = default);
}

/// <summary>Stored as PER_KG / PER_ITEM / FLAT.</summary>
public enum ChargeBasis
{
    PerKg,
    PerItem,
    Flat,
}

public sealed record PriceKey(Guid VariantId, Guid? PreparationMethodId);

/// <param name="UnitPricePaise">Price per sell unit (per kg, piece or pack).</param>
/// <param name="ChargeBasis">Basis of <paramref name="PreparationChargePaise"/>; null when there is no preparation charge.</param>
public sealed record LinePrice(
    Guid VariantId,
    Guid? PreparationMethodId,
    long UnitPricePaise,
    long? CompareAtPaise,
    long PreparationChargePaise,
    ChargeBasis? ChargeBasis,
    int TaxRateBp);
