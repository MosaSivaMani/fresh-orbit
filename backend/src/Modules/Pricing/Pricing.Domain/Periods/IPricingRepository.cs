namespace FreshOrbit.Pricing.Domain.Periods;

/// <summary>Loads whole schedules (all periods of the keys) in one query per kind, and stages changes for one save.</summary>
public interface IPricingRepository
{
    Task<List<VariantPrice>> GetPricesAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken);

    Task<List<PreparationCharge>> GetChargesAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken);

    Task<List<TaxRule>> GetTaxRulesAsync(IReadOnlyCollection<string> taxCategoryCodes, CancellationToken cancellationToken);

    Task<bool> TaxCategoryExistsAsync(string code, CancellationToken cancellationToken);

    Task<List<TaxCategory>> ListTaxCategoriesAsync(CancellationToken cancellationToken);

    /// <summary>Variants with a CURRENT or FUTURE price at the location at <paramref name="at"/>.</summary>
    Task<List<Guid>> GetPricedVariantIdsAsync(Guid fulfillmentLocationId, DateTimeOffset at, CancellationToken cancellationToken);

    void Add(PricePeriod period);

    void Remove(PricePeriod period);

    /// <summary>Marks the row changed so its version is checked and bumped even when no column changed.</summary>
    void MarkChanged(PricePeriod period);
}
