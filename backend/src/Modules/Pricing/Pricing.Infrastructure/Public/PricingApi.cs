using FreshOrbit.Catalog.Public;
using FreshOrbit.Pricing.Application.Common;
using FreshOrbit.Pricing.Domain.Periods;
using FreshOrbit.Pricing.Domain.Shared;
using FreshOrbit.Pricing.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Pricing.Infrastructure.Public;

/// <summary><see cref="IPricingApi"/>: one Catalog call plus one query per table per call, never per variant.</summary>
public sealed class PricingApi(FreshOrbitDbContext db, ICatalogApi catalog) : IPricingApi
{
    public async Task<IReadOnlyList<LinePrice>> GetLinePricesAsync(Guid fulfillmentLocationId, IReadOnlyCollection<PriceKey> keys, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var snapshot = await LoadAsync(fulfillmentLocationId, [.. keys.Select(k => k.VariantId).Distinct()], at, cancellationToken);
        var result = new List<LinePrice>();
        foreach (var key in keys)
        {
            if (!snapshot.Variants.TryGetValue(key.VariantId, out var variant)
                || !snapshot.Prices.TryGetValue(key.VariantId, out var price)
                || !snapshot.TaxRates.TryGetValue(variant.TaxCategoryCode, out var rate))
            {
                continue;
            }

            if (key.PreparationMethodId is not { } methodId)
            {
                result.Add(new LinePrice(key.VariantId, null, price.UnitPricePaise, price.CompareAtPaise, 0, null, rate));
                continue;
            }

            if (!variant.Preparations.Any(p => p.PreparationMethodId == methodId && p.IsActive)
                || !snapshot.Charges.TryGetValue((key.VariantId, methodId), out var charge))
            {
                continue;
            }

            result.Add(new LinePrice(key.VariantId, methodId, price.UnitPricePaise, price.CompareAtPaise, charge.ChargePaise, PricingRules.ToPublic(charge.ChargeBasis), rate));
        }

        return result;
    }

    public async Task<IReadOnlyList<VariantOffer>> GetOffersAsync(Guid fulfillmentLocationId, IReadOnlyCollection<Guid> variantIds, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(variantIds);
        var ids = variantIds.Distinct().ToList();
        var snapshot = await LoadAsync(fulfillmentLocationId, ids, at, cancellationToken);
        return [.. ids.Where(snapshot.Variants.ContainsKey).Select(id =>
        {
            var variant = snapshot.Variants[id];
            snapshot.Prices.TryGetValue(id, out var price);
            int? rate = snapshot.TaxRates.TryGetValue(variant.TaxCategoryCode, out var r) ? r : null;
            var issue = !variant.IsActive || variant.ProductStatus != ProductStatus.Active ? PricingErrors.ProductNotAvailable
                : price is null ? PricingErrors.PriceNotAvailable
                : rate is null ? PricingErrors.TaxRuleMissing
                : null;
            var preparations = variant.Preparations.Where(p => p.IsActive).Select(p =>
            {
                var hasCharge = snapshot.Charges.TryGetValue((id, p.PreparationMethodId), out var charge);
                return new PreparationOffer(
                    p.PreparationMethodId,
                    p.Code,
                    p.IsDefault,
                    hasCharge ? charge!.ChargePaise : null,
                    hasCharge ? PricingRules.ToPublic(charge!.ChargeBasis) : null,
                    hasCharge ? null : PricingErrors.PreparationChargeNotAvailable);
            }).ToList();
            return new VariantOffer(id, issue, price?.UnitPricePaise, price?.CompareAtPaise, PricingRules.PriceUnit(variant.SellUnit), rate, preparations);
        })];
    }

    public async Task<IReadOnlyDictionary<string, int>> GetTaxRatesAsync(IReadOnlyCollection<string> taxCategoryCodes, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxCategoryCodes);
        var codes = taxCategoryCodes.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        if (codes.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var rules = await db.Set<TaxRule>().AsNoTracking()
            .Where(r => codes.Contains(r.TaxCategoryCode) && r.ValidFrom <= at && (r.ValidTo == null || r.ValidTo > at))
            .Select(r => new { r.TaxCategoryCode, r.RateBp })
            .ToListAsync(cancellationToken);
        return rules.ToDictionary(r => r.TaxCategoryCode, r => r.RateBp, StringComparer.Ordinal);
    }

    private async Task<Snapshot> LoadAsync(Guid locationId, List<Guid> variantIds, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (variantIds.Count == 0)
        {
            return new Snapshot([], [], [], new Dictionary<string, int>(StringComparer.Ordinal));
        }

        var variants = (await catalog.GetVariantsAsync(variantIds, cancellationToken)).ToDictionary(v => v.VariantId);
        var prices = await db.Set<VariantPrice>().AsNoTracking()
            .Where(p => p.FulfillmentLocationId == locationId && variantIds.Contains(p.VariantId) && p.ValidFrom <= at && (p.ValidTo == null || p.ValidTo > at))
            .ToDictionaryAsync(p => p.VariantId, cancellationToken);
        var charges = await db.Set<PreparationCharge>().AsNoTracking()
            .Where(c => c.FulfillmentLocationId == locationId && variantIds.Contains(c.VariantId) && c.ValidFrom <= at && (c.ValidTo == null || c.ValidTo > at))
            .ToDictionaryAsync(c => (c.VariantId, c.PreparationMethodId), cancellationToken);
        var rates = await GetTaxRatesAsync([.. variants.Values.Select(v => v.TaxCategoryCode)], at, cancellationToken);
        return new Snapshot(variants, prices, charges, rates);
    }

    private sealed record Snapshot(
        Dictionary<Guid, VariantInfo> Variants,
        Dictionary<Guid, VariantPrice> Prices,
        Dictionary<(Guid VariantId, Guid MethodId), PreparationCharge> Charges,
        IReadOnlyDictionary<string, int> TaxRates);
}
