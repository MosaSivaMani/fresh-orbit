using FreshOrbit.Catalog.Public;
using FreshOrbit.Locations.Public;

namespace FreshOrbit.Promotions.Application.Abstractions;

/// <summary><see cref="IPromotionsCatalog"/> over <c>Catalog.Public</c>.</summary>
public sealed class CatalogPromotionsCatalog(ICatalogApi catalog) : IPromotionsCatalog
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetCategoryLineagesAsync(IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken)
    {
        var lineages = await catalog.GetCategoryLineagesAsync(categoryIds, cancellationToken);
        return lineages.ToDictionary(l => l.Key, l => (IReadOnlyCollection<Guid>)l.Value);
    }

    public async Task<IReadOnlyCollection<Guid>> FindMissingAsync(IReadOnlyCollection<Guid> categoryIds, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(categoryIds);
        ArgumentNullException.ThrowIfNull(productIds);
        var missing = new List<Guid>();
        if (categoryIds.Count > 0)
        {
            var found = await catalog.GetCategoryLineagesAsync(categoryIds, cancellationToken);
            missing.AddRange(categoryIds.Distinct().Where(id => !found.ContainsKey(id)));
        }

        if (productIds.Count > 0)
        {
            var found = (await catalog.GetProductSummariesAsync(productIds, cancellationToken)).Select(p => p.ProductId).ToHashSet();
            missing.AddRange(productIds.Distinct().Where(id => !found.Contains(id)));
        }

        return missing;
    }
}

/// <summary><see cref="ICityDirectory"/> over <c>Locations.Public</c>.</summary>
public sealed class LocationsCityDirectory(ILocationsApi locations) : ICityDirectory
{
    public async Task<bool> CityExistsAsync(Guid cityId, CancellationToken cancellationToken)
        => (await locations.GetCitiesAsync([cityId], cancellationToken)).Count > 0;
}
