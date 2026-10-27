namespace FreshOrbit.Catalog.Public;

/// <summary>
/// The banners <c>GET /home</c> shows. Banners belong to Promotions, which uses Catalog, so Catalog cannot call it;
/// Promotions implements this interface instead (the pattern of <c>ICityActivationCheck</c>, docs/architecture
/// "Dependency exceptions"). Without an implementation the home page has no banners.
/// </summary>
public interface IHomeBannerSource
{
    /// <summary>
    /// Banners shown at <paramref name="at"/> for the city of <paramref name="fulfillmentLocationId"/> plus all-city
    /// banners, by sort order; all-city banners only when the location is null (no location context).
    /// </summary>
    Task<IReadOnlyList<HomeBanner>> GetHomeBannersAsync(Guid? fulfillmentLocationId, DateTimeOffset at, CancellationToken cancellationToken = default);
}

/// <summary>A home banner; the same fields as <c>GET /promotions</c> items.</summary>
/// <param name="ImageKey">Storage key of the banner image (kept for existing clients).</param>
/// <param name="ImageUrl">Public URL of <paramref name="ImageKey"/> from the file storage; null without an image.</param>
/// <param name="LinkType">CATEGORY / PRODUCT / COUPON.</param>
public sealed record HomeBanner(
    Guid Id,
    string Title,
    string? Subtitle,
    string? ImageKey,
    string? ImageUrl,
    string LinkType,
    string LinkTarget,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    int SortOrder)
{
    /// <summary>Short label the admin set for the banner ("NEW", "20% OFF"); null when none. Show it as given.</summary>
    public string? Badge { get; init; }

    /// <summary>Slug of the CATEGORY or PRODUCT link target (W14); null for COUPON or a missing target. <c>LinkTarget</c> stays the id.</summary>
    public string? LinkSlug { get; init; }
}
