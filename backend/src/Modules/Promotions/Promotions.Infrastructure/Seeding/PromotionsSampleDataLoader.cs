using FreshOrbit.Catalog.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Promotions.Application.Banners;
using FreshOrbit.Promotions.Application.Coupons;
using FreshOrbit.Promotions.Domain.Banners;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Promotions.Infrastructure.Seeding;

/// <summary>
/// Sample <c>promotions.json</c> (order 80) through <see cref="CouponAdminService"/> and <see cref="BannerService"/>.
/// Natural keys become ids: <c>cityCode</c> → city id, coupon scope and banner CATEGORY / PRODUCT targets (slugs) →
/// category / product ids; a COUPON target stays the code. Offsets: days from the city-local date of T0 (midnight),
/// hours from T0. Coupons first, so banner coupon links resolve. Existing coupon codes and banner titles are skipped;
/// a banner's image file (written by the Catalog loader from images.json) must exist.
/// </summary>
public sealed partial class PromotionsSampleDataLoader(
    CouponAdminService coupons,
    BannerService banners,
    ICatalogApi catalog,
    ILocationsApi locations,
    IFileStorage storage,
    FreshOrbitDbContext db,
    ILogger<PromotionsSampleDataLoader> logger) : ISampleDataLoader
{
    private const string FileName = "promotions.json";

    public int Order => 80;

    public async Task LoadAsync(SampleDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var file = await SampleFiles.ReadAsync<PromotionsFile>(context, FileName, cancellationToken);
        var cityCodes = file.Coupons.Select(c => c.CityCode).Concat(file.Banners.Select(b => b.CityCode)).OfType<string>().Distinct().ToList();
        var cities = (await locations.GetCitiesByCodesAsync(cityCodes, cancellationToken)).ToDictionary(c => c.Code, StringComparer.Ordinal);
        var categorySlugs = file.Coupons.SelectMany(c => c.Scopes).Where(s => s.ScopeType == CouponScopeType.Category).Select(s => s.ScopeRef)
            .Concat(file.Banners.Where(b => b.LinkType == PromotionLinkType.Category).Select(b => b.LinkTarget)).Distinct().ToList();
        var productSlugs = file.Coupons.SelectMany(c => c.Scopes).Where(s => s.ScopeType == CouponScopeType.Product).Select(s => s.ScopeRef)
            .Concat(file.Banners.Where(b => b.LinkType == PromotionLinkType.Product).Select(b => b.LinkTarget)).Distinct().ToList();
        var categoryIds = categorySlugs.Count == 0 ? new Dictionary<string, Guid>() : await catalog.GetCategoryIdsBySlugsAsync(categorySlugs, cancellationToken);
        var productIds = productSlugs.Count == 0 ? new Dictionary<string, Guid>() : await catalog.GetProductIdsBySlugsAsync(productSlugs, cancellationToken);
        CitySummary? City(string? code) => code is null ? null : cities.TryGetValue(code, out var city) ? city : throw SampleFiles.Missing(FileName, "city", code);
        Guid Target(PromotionLinkType type, string slug) => type == PromotionLinkType.Category
            ? categoryIds.TryGetValue(slug, out var c) ? c : throw SampleFiles.Missing(FileName, "category", slug)
            : productIds.TryGetValue(slug, out var p) ? p : throw SampleFiles.Missing(FileName, "product", slug);

        var codes = file.Coupons.Select(c => Coupon.NormalizeCode(c.Code)).ToList();
        var existingCodes = (await db.Set<Coupon>().AsNoTracking().Where(c => codes.Contains(c.Code)).Select(c => c.Code).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var insertedCoupons = 0;
        foreach (var row in file.Coupons.Where(c => !existingCodes.Contains(Coupon.NormalizeCode(c.Code))))
        {
            var city = City(row.CityCode);
            var timeZone = city?.TimeZone ?? throw new InvalidDataException($"{FileName}: coupon {row.Code} needs a cityCode; its day offsets are city-local dates.");
            var request = new CouponRequest(
                row.Code,
                row.Description,
                row.DiscountType,
                row.DiscountValue,
                row.MaxDiscountPaise,
                row.MinOrderPaise,
                city?.Id,
                row.FirstOrderOnly,
                row.PerCustomerLimit,
                row.TotalLimit,
                SampleFiles.LocalMidnight(context.StartedAt, timeZone, row.ValidFromOffsetDays),
                row.ValidToOffsetDays is { } to ? SampleFiles.LocalMidnight(context.StartedAt, timeZone, to) : null,
                row.IsListed,
                [.. row.Scopes.Select(s => new CouponScopeDto(s.ScopeType, Target(s.ScopeType == CouponScopeType.Category ? PromotionLinkType.Category : PromotionLinkType.Product, s.ScopeRef)))]);
            var coupon = await coupons.CreateAsync(request, cancellationToken);
            context.Track<Coupon>(db, coupon.Id);
            insertedCoupons++;
        }

        var titles = file.Banners.Select(b => b.Title).ToList();
        var existingBanners = (await db.Set<Promotion>().AsNoTracking().Where(p => titles.Contains(p.Title)).Select(p => new { p.Title, p.CityId }).ToListAsync(cancellationToken))
            .Select(p => (p.Title, p.CityId)).ToHashSet();
        var insertedBanners = 0;
        foreach (var row in file.Banners)
        {
            var city = City(row.CityCode);
            if (existingBanners.Contains((row.Title, city?.Id)))
            {
                continue;
            }

            if (row.ImageKey is not null && !await storage.ExistsAsync(row.ImageKey, cancellationToken))
            {
                throw new InvalidDataException($"{FileName}: banner {row.Key} image '{row.ImageKey}' is missing; the Catalog loader renders it from images.json.");
            }

            var target = row.LinkType == PromotionLinkType.Coupon ? row.LinkTarget : Target(row.LinkType, row.LinkTarget).ToString();
            var banner = await banners.CreateAsync(
                new PromotionRequest(
                    row.Title,
                    row.Subtitle,
                    row.ImageKey,
                    row.LinkType,
                    target,
                    city?.Id,
                    context.StartedAt.AddHours(row.StartsAtOffsetHours),
                    row.EndsAtOffsetHours is { } end ? context.StartedAt.AddHours(end) : null,
                    row.SortOrder,
                    row.IsActive,
                    row.Badge),
                cancellationToken);
            context.Track<Promotion>(db, banner.Id);
            insertedBanners++;
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded(insertedCoupons, insertedBanners);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample promotions inserted: {Coupons} coupons, {Banners} banners (existing skipped)")]
    private partial void LogLoaded(int coupons, int banners);

    private sealed record PromotionsFile(IReadOnlyList<CouponRow> Coupons, IReadOnlyList<BannerRow> Banners);

    private sealed record CouponRow(
        string Code,
        string? Description,
        DiscountType DiscountType,
        long DiscountValue,
        long? MaxDiscountPaise,
        long MinOrderPaise,
        string? CityCode,
        bool FirstOrderOnly,
        int? PerCustomerLimit,
        int? TotalLimit,
        bool IsListed,
        int ValidFromOffsetDays,
        int? ValidToOffsetDays,
        IReadOnlyList<ScopeRow> Scopes);

    private sealed record ScopeRow(CouponScopeType ScopeType, string ScopeRef);

    private sealed record BannerRow(
        string Key,
        string Title,
        string? Subtitle,
        string? ImageKey,
        PromotionLinkType LinkType,
        string LinkTarget,
        string? CityCode,
        int StartsAtOffsetHours,
        int? EndsAtOffsetHours,
        int SortOrder,
        bool IsActive,
        string? Badge = null);
}
