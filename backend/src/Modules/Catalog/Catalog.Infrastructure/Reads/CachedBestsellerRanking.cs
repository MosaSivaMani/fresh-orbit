using FreshOrbit.Catalog.Application.Storefront;
using FreshOrbit.Catalog.Public;
using Microsoft.Extensions.Caching.Memory;

namespace FreshOrbit.Catalog.Infrastructure.Reads;

/// <summary>
/// CAT-R24: the sales ranking of the last <see cref="Window"/> per location from every <see cref="IBestsellerSource"/>,
/// kept in memory for <see cref="CacheFor"/> (a ranking does not need to move with every order). Only product ids are
/// cached; visibility, price and stock are read fresh on every request.
/// </summary>
public sealed class CachedBestsellerRanking(IEnumerable<IBestsellerSource> sources, IMemoryCache cache) : IBestsellerRanking
{
    public const int RankedLimit = 40;
    public static readonly TimeSpan Window = TimeSpan.FromDays(30);
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlyList<Guid>> GetProductIdsAsync(Guid fulfillmentLocationId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var key = $"catalog:bestsellers:{fulfillmentLocationId:N}";
        if (cache.TryGetValue(key, out IReadOnlyList<Guid>? cached) && cached is not null)
        {
            return cached;
        }

        var ids = new List<Guid>();
        foreach (var source in sources)
        {
            ids.AddRange((await source.GetBestsellingProductIdsAsync(fulfillmentLocationId, at - Window, RankedLimit, cancellationToken)).Where(id => !ids.Contains(id)));
        }

        cache.Set(key, (IReadOnlyList<Guid>)ids, CacheFor);
        return ids;
    }
}
