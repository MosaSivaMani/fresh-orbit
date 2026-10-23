namespace FreshOrbit.Pricing.Public;

/// <summary>What other modules may ask the Pricing module.</summary>
public interface IPricingApi
{
    // Members added in Phase 2 have default bodies so test doubles written against the Phase 1 contract still compile;
    // the module's real implementation overrides every one of them.
    private const string NewMemberNote = "Not implemented by this test double.";

    /// <summary>
    /// Prices valid at <paramref name="at"/> at the location. A key without a price is absent from the result:
    /// it is not for sale there. A key is also absent when its preparation has no charge row (PRC-R8), the preparation
    /// is not enabled on the variant, or the product's tax category has no tax rule at <paramref name="at"/> (PRC-R9).
    /// A key without a preparation gets <c>PreparationChargePaise = 0</c> and <c>ChargeBasis = null</c>.
    /// One query per table per call, never per key.
    /// </summary>
    Task<IReadOnlyList<LinePrice>> GetLinePricesAsync(Guid fulfillmentLocationId, IReadOnlyCollection<PriceKey> keys, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything needed to price the variants at the location at <paramref name="at"/>: price, charge per enabled
    /// preparation and tax rate, or the reason it cannot be sold (<see cref="VariantOffer.IssueCode"/>,
    /// <see cref="PreparationOffer.IssueCode"/>). One entry per requested variant that exists in the catalog.
    /// </summary>
    Task<IReadOnlyList<VariantOffer>> GetOffersAsync(Guid fulfillmentLocationId, IReadOnlyCollection<Guid> variantIds, DateTimeOffset at, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>Tax rate in force at <paramref name="at"/> per tax category code; a code without a rule is absent (TAX_RULE_MISSING).</summary>
    Task<IReadOnlyDictionary<string, int>> GetTaxRatesAsync(IReadOnlyCollection<string> taxCategoryCodes, DateTimeOffset at, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);
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

/// <summary>A variant's price at a location. Prices are tax-inclusive (PRC-R10).</summary>
/// <param name="IssueCode">Null when sellable; else PRICE_NOT_AVAILABLE, TAX_RULE_MISSING or PRODUCT_NOT_AVAILABLE.</param>
/// <param name="UnitPricePaise">Null when there is no current price.</param>
/// <param name="TaxRateBp">Null when the tax category has no rule.</param>
public sealed record VariantOffer(
    Guid VariantId,
    string? IssueCode,
    long? UnitPricePaise,
    long? CompareAtPaise,
    string PriceUnit,
    int? TaxRateBp,
    IReadOnlyList<PreparationOffer> Preparations);

/// <summary>Charge of one enabled preparation of a variant.</summary>
/// <param name="IssueCode">Null when a charge exists; else PREPARATION_CHARGE_NOT_AVAILABLE.</param>
public sealed record PreparationOffer(
    Guid PreparationMethodId,
    string PreparationCode,
    bool IsDefault,
    long? ChargePaise,
    ChargeBasis? ChargeBasis,
    string? IssueCode);
