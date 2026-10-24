using FreshOrbit.Catalog.Public;

namespace FreshOrbit.Catalog.Application.Storefront;

/// <summary>Product ids ranked by real sales at a location (CAT-R24), cached by the infrastructure.</summary>
public interface IBestsellerRanking
{
    Task<IReadOnlyList<Guid>> GetProductIdsAsync(Guid fulfillmentLocationId, DateTimeOffset at, CancellationToken cancellationToken);
}

/// <summary>
/// Adds facts owned by other modules to browse responses through the Catalog.Public sources they implement: freshness
/// (Inventory, CAT-R25), the offer line (Promotions, CAT-R26) and bestsellers (Ordering, CAT-R24). One call per source
/// per response, never per product. Without a location, or with no source registered, the facts stay null.
/// </summary>
public sealed class StorefrontEnricher(
    IStorefrontReads reads,
    IBestsellerRanking bestsellers,
    IEnumerable<IVariantFreshnessSource> freshnessSources,
    IEnumerable<IProductOfferSource> offerSources)
{
    public const int BestsellerLimit = 12;

    public async Task<IReadOnlyList<ProductListItem>> EnrichAsync(IReadOnlyList<ProductListItem> items, StorefrontLocation? location, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (location is null || items.Count == 0)
        {
            return items;
        }

        var variantIds = items.Where(i => i.DefaultVariant is not null).Select(i => i.DefaultVariant!.Id).Distinct().ToList();
        var freshness = await FreshnessAsync(location, variantIds, at, cancellationToken);
        var offers = await OffersAsync(location, [.. items.Select(i => new ProductOfferQuery(i.Id, i.CategoryId))], at, cancellationToken);
        return [.. items.Select(i => i with
        {
            Freshness = i.DefaultVariant is { } v && freshness.TryGetValue(v.Id, out var f) ? f : null,
            Offer = offers.GetValueOrDefault(i.Id),
        })];
    }

    public async Task<StorefrontProductDetail> EnrichAsync(StorefrontProductDetail product, StorefrontLocation? location, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (location is null)
        {
            return product;
        }

        var freshness = await FreshnessAsync(location, [.. product.Variants.Select(v => v.Id)], at, cancellationToken);
        var offers = await OffersAsync(location, [new ProductOfferQuery(product.Id, product.CategoryId)], at, cancellationToken);
        return product with
        {
            Variants = [.. product.Variants.Select(v => v with { Freshness = freshness.GetValueOrDefault(v.Id) })],
            Offer = offers.GetValueOrDefault(product.Id),
        };
    }

    /// <summary>Best sellers at the location that are sellable now, best first, enriched; empty when nothing sold.</summary>
    public async Task<IReadOnlyList<ProductListItem>> BestsellersAsync(StorefrontLocation location, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        var ids = await bestsellers.GetProductIdsAsync(location.FulfillmentLocationId, at, cancellationToken);
        if (ids.Count == 0)
        {
            return [];
        }

        var items = await reads.ListProductsByIdsAsync(ids, location, at, cancellationToken);
        return await EnrichAsync([.. items.Take(BestsellerLimit)], location, at, cancellationToken);
    }

    private async Task<Dictionary<Guid, FreshnessInfo>> FreshnessAsync(StorefrontLocation location, List<Guid> variantIds, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, FreshnessInfo>();
        if (variantIds.Count == 0)
        {
            return result;
        }

        foreach (var source in freshnessSources)
        {
            foreach (var row in await source.GetFreshnessAsync(location.FulfillmentLocationId, variantIds, at, cancellationToken))
            {
                result.TryAdd(row.VariantId, new FreshnessInfo(row.CaughtOn, row.ReceivedAt));
            }
        }

        return result;
    }

    private async Task<Dictionary<Guid, ProductOffer>> OffersAsync(StorefrontLocation location, List<ProductOfferQuery> products, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, ProductOffer>();
        if (location.CityId is not { } cityId || products.Count == 0)
        {
            return result;
        }

        foreach (var source in offerSources)
        {
            foreach (var line in await source.GetOfferLinesAsync(cityId, products, at, cancellationToken))
            {
                result.TryAdd(line.ProductId, new ProductOffer(line.CouponCode, line.Text));
            }
        }

        return result;
    }
}
