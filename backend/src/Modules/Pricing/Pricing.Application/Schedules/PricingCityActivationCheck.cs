using FreshOrbit.Catalog.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Pricing.Domain.Periods;
using FreshOrbit.Pricing.Domain.Shared;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Pricing.Application.Schedules;

/// <summary>
/// PRC-R12 (decision D4): a location is ready when at least one variant that is active on an ACTIVE product has a
/// CURRENT price, a CURRENT charge for its default preparation and a CURRENT tax rule; otherwise PRICE_MISSING for it.
/// </summary>
public sealed class PricingCityActivationCheck(IPricingRepository repository, ICatalogApi catalog, ILocationsApi locations, IClock clock) : ICityActivationCheck
{
    public const string MissingMessage = "No variant has a current price, a charge for its default preparation and a tax rule at this location.";

    public async Task<IReadOnlyList<string>> GetMissingItemsAsync(Guid cityId, CancellationToken cancellationToken = default)
    {
        var cityLocations = await locations.GetCityFulfillmentLocationsAsync(cityId, cancellationToken);
        var active = cityLocations.Where(l => l.Status == FulfillmentLocationStatus.Active).Select(l => l.Id).ToList();
        var missing = await GetMissingDetailsAsync(cityId, active, cancellationToken);
        return [.. missing.Select(m => m.Code).Distinct()];
    }

    public async Task<IReadOnlyList<CityMissingItem>> GetMissingDetailsAsync(Guid cityId, IReadOnlyCollection<Guid> activeLocationIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activeLocationIds);
        if (activeLocationIds.Count == 0)
        {
            return [];
        }

        var now = clock.UtcNow;
        var sellable = await catalog.GetSellableVariantIdsAsync(cancellationToken);
        var variants = sellable.Count == 0 ? [] : await catalog.GetVariantsAsync(sellable, cancellationToken);
        var candidates = variants
            .Select(v => (Variant: v, Default: v.Preparations.FirstOrDefault(p => p.IsActive && p.IsDefault)))
            .Where(x => x.Default is not null && x.Variant.TaxCategoryCode.Length > 0)
            .ToList();
        if (candidates.Count == 0)
        {
            return [.. activeLocationIds.Select(Missing)];
        }

        var variantIds = candidates.Select(c => c.Variant.VariantId).ToList();
        var prices = await repository.GetPricesAsync(activeLocationIds, variantIds, cancellationToken);
        var charges = await repository.GetChargesAsync(activeLocationIds, variantIds, cancellationToken);
        var taxed = (await repository.GetTaxRulesAsync([.. candidates.Select(c => c.Variant.TaxCategoryCode).Distinct()], cancellationToken))
            .Where(r => r.IsCurrentAt(now))
            .Select(r => r.TaxCategoryCode)
            .ToHashSet(StringComparer.Ordinal);

        var priced = prices.Where(p => p.IsCurrentAt(now)).Select(p => (p.FulfillmentLocationId, p.VariantId)).ToHashSet();
        var charged = charges.Where(c => c.IsCurrentAt(now)).Select(c => (c.FulfillmentLocationId, c.VariantId, c.PreparationMethodId)).ToHashSet();
        return [.. activeLocationIds
            .Where(location => !candidates.Any(c =>
                taxed.Contains(c.Variant.TaxCategoryCode)
                && priced.Contains((location, c.Variant.VariantId))
                && charged.Contains((location, c.Variant.VariantId, c.Default!.PreparationMethodId))))
            .Select(Missing)];
    }

    private static CityMissingItem Missing(Guid locationId) => new(PricingErrors.PriceMissing, locationId, MissingMessage);
}
