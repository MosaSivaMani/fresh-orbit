using FreshOrbit.Catalog.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Promotions.Public;

namespace FreshOrbit.Promotions.Infrastructure.Public;

/// <summary>
/// <c>GET /home</c> banners for Catalog (W11): the same banners and image URLs as <c>GET /promotions</c>, for the
/// city of the fulfillment location Catalog resolved from the pincode. A location Locations no longer knows shows
/// all-city banners only.
/// </summary>
internal sealed class PromotionsHomeBannerSource(IPromotionsApi promotions, ILocationsApi locations) : IHomeBannerSource
{
    public async Task<IReadOnlyList<HomeBanner>> GetHomeBannersAsync(Guid? fulfillmentLocationId, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        Guid? cityId = null;
        if (fulfillmentLocationId is { } locationId)
        {
            var found = await locations.GetFulfillmentLocationsAsync([locationId], cancellationToken);
            cityId = found.Count > 0 ? found[0].CityId : null;
        }

        var banners = await promotions.GetActiveBannersAsync(cityId, at, cancellationToken);
        return [.. banners.Select(b => new HomeBanner(b.Id, b.Title, b.Subtitle, b.ImageKey, b.ImageUrl, b.LinkType, b.LinkTarget, b.StartsAt, b.EndsAt, b.SortOrder) { LinkSlug = b.LinkSlug, Badge = b.Badge })];
    }
}
