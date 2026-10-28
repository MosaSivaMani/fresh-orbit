using System.Globalization;
using FreshOrbit.Catalog.Application.Admin;
using FreshOrbit.Catalog.Domain.Categories;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace FreshOrbit.Catalog.Infrastructure.Seeding;

/// <summary>
/// Sample <c>catalog.json</c> and <c>images.json</c> (order 30), through the Catalog services: categories (with the
/// committed picture of <see cref="SampleImageFiles"/>, else a rendered placeholder), products, variants, the committed
/// product pictures of <see cref="SampleProductPictures"/> (else one rendered placeholder) uploaded through
/// <see cref="ProductImageService"/> (CAT-R16 renditions, like an admin upload), then publish (<c>targetStatus: ACTIVE</c>, CAT-R3).
/// Also writes the banner image files of images.json, real pictures where committed (the Promotions loader links them).
/// Existing slugs are skipped.
/// </summary>
public sealed partial class CatalogSampleDataLoader(
    CategoryService categories,
    ProductService products,
    ProductImageService images,
    IFileStorage storage,
    FreshOrbitDbContext db,
    ILogger<CatalogSampleDataLoader> logger) : ISampleDataLoader
{
    private const string FileName = "catalog.json";

    public int Order => 30;

    public async Task LoadAsync(SampleDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var file = await SampleFiles.ReadAsync<CatalogFile>(context, FileName, cancellationToken);
        var pictures = await SampleFiles.ReadAsync<ImagesFile>(context, "images.json", cancellationToken);

        // Categories: parents first, then children.
        var slugs = file.Categories.Select(c => c.Slug).ToList();
        var categoryIds = await db.Set<Category>().AsNoTracking().Where(c => slugs.Contains(c.Slug)).ToDictionaryAsync(c => c.Slug, c => c.Id, cancellationToken);
        var insertedCategories = 0;
        foreach (var row in file.Categories.OrderBy(c => c.ParentSlug is null ? 0 : 1))
        {
            if (categoryIds.ContainsKey(row.Slug))
            {
                continue;
            }

            Guid? parentId = row.ParentSlug is null ? null : categoryIds.TryGetValue(row.ParentSlug, out var pid) ? pid : throw SampleFiles.Missing(FileName, "category", row.ParentSlug);
            var created = await categories.CreateAsync(new CreateCategoryRequest(parentId, row.Slug, row.Name, row.Description, row.SortOrder), cancellationToken);
            context.Track<Category>(db, created.Id);
            categoryIds[row.Slug] = created.Id;
            insertedCategories++;

            var picture = pictures.Categories.FirstOrDefault(p => p.CategorySlug == row.Slug);
            var imageKey = row.ImageKey ?? picture?.ImageKey;
            if (imageKey is not null)
            {
                var placeholder = picture?.Placeholder ?? new Placeholder(1200, 800);
                await SaveFileAsync(context, imageKey, placeholder, SamplePlaceholderRenderer.ShapeFor(row.Slug), cancellationToken);
            }

            if (imageKey is not null || !row.IsActive)
            {
                await categories.UpdateAsync(
                    created.Id,
                    uint.Parse(created.Version, CultureInfo.InvariantCulture),
                    new UpdateCategoryRequest(parentId, row.Slug, row.Name, row.Description, imageKey, row.SortOrder, row.IsActive),
                    cancellationToken);
            }
        }

        // Banner image files (no rows; Promotions links them by key).
        foreach (var banner in pictures.Banners)
        {
            if (!await storage.ExistsAsync(banner.ImageKey, cancellationToken))
            {
                await SaveFileAsync(context, banner.ImageKey, banner.Placeholder, SamplePlaceholderRenderer.ShapeFor(banner.ImageKey), cancellationToken);
            }
        }

        // Products, variants, image, publish.
        var productSlugs = file.Products.Select(p => p.Slug).ToList();
        var existingProducts = (await db.Set<Product>().AsNoTracking().Where(p => productSlugs.Contains(p.Slug)).Select(p => p.Slug).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var insertedProducts = 0;
        var insertedVariants = 0;
        var published = 0;
        foreach (var row in file.Products.Where(p => !existingProducts.Contains(p.Slug)))
        {
            var categoryId = categoryIds.TryGetValue(row.CategorySlug, out var cid) ? cid : throw SampleFiles.Missing(FileName, "category", row.CategorySlug);
            var product = await products.CreateAsync(
                new ProductRequest(categoryId, row.Slug, row.Name, row.LocalNames, row.SpeciesScientificName, row.Description, row.SourceType, row.SourceRegion, row.StorageInstructions, row.HandlingNotes, row.HsnCode, row.TaxCategoryCode, row.FeaturedRank),
                cancellationToken);
            context.Track<Product>(db, product.Id);
            insertedProducts++;
            var version = Version(product.Version);
            foreach (var variant in row.Variants)
            {
                var withVariant = await products.AddVariantAsync(
                    product.Id,
                    version,
                    new VariantRequest(
                        variant.Sku,
                        variant.Name,
                        variant.SellUnit,
                        variant.PackSizeGrams,
                        variant.AvgPieceGrams,
                        variant.MinOrder,
                        variant.MaxOrder,
                        variant.OrderStep,
                        variant.IsActive,
                        [.. variant.Preparations.Select(p => new VariantPreparationRequest(p.Code, p.YieldBp, p.IsDefault))],
                        variant.PiecesMin,
                        variant.PiecesMax,
                        variant.Serves),
                    cancellationToken);
                version = Version(withVariant.Version);
                context.Track<ProductVariant>(db, withVariant.Variants.Single(v => string.Equals(v.Sku, variant.Sku, StringComparison.OrdinalIgnoreCase)).Id);
                insertedVariants++;
            }

            // D249: the committed pictures in number order (1 = main), else the rendered placeholder.
            var picture = pictures.Products.FirstOrDefault(p => p.ProductSlug == row.Slug);
            var committed = SampleProductPictures.Find(context.DirectoryPath, row.Slug, (picture?.Pictures ?? []).ToDictionary(p => p.Number, p => p.AltText));
            var uploads = new List<(byte[] Bytes, string AltText)>();
            foreach (var committedPicture in committed)
            {
                uploads.Add((await File.ReadAllBytesAsync(committedPicture.FilePath, cancellationToken), committedPicture.AltText));
            }

            if (uploads.Count == 0 && picture is not null)
            {
                var placeholder = picture.Placeholder;
                uploads.Add((SamplePlaceholderRenderer.Render(placeholder.Width, placeholder.Height, SamplePlaceholderRenderer.ShapeFor(row.CategorySlug, row.Slug), SKEncodedImageFormat.Png), placeholder.AltText));
            }

            foreach (var (bytes, altText) in uploads)
            {
                using var stream = new MemoryStream(bytes, writable: false);
                var (uploaded, productVersion) = await images.UploadAsync(product.Id, stream, altText, cancellationToken);
                context.Track<ProductImage>(db, uploaded.Id);
                version = productVersion;
            }

            if (row.TargetStatus == "ACTIVE")
            {
                await products.PublishAsync(product.Id, version, cancellationToken);
                published++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded(insertedCategories, insertedProducts, insertedVariants, published);
    }

    private static uint Version(string version) => uint.Parse(version, CultureInfo.InvariantCulture);

    private async Task SaveFileAsync(SampleDataContext context, string key, Placeholder placeholder, PlaceholderShape shape, CancellationToken cancellationToken)
    {
        StorageKeys.EnsureValid(key);
        var format = key.EndsWith(".png", StringComparison.Ordinal) ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Webp;
        var file = SampleImageFiles.Find(context.DirectoryPath, key);
        var bytes = file is not null
            ? await File.ReadAllBytesAsync(file, cancellationToken)
            : SamplePlaceholderRenderer.Render(placeholder.Width, placeholder.Height, shape, format);
        using var stream = new MemoryStream(bytes, writable: false);
        await storage.SaveAsync(key, stream, format == SKEncodedImageFormat.Png ? "image/png" : ImageRenditions.WebPContentType, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample catalog inserted: {Categories} categories, {Products} products, {Variants} variants, {Published} published (existing slugs skipped)")]
    private partial void LogLoaded(int categories, int products, int variants, int published);

    private sealed record CatalogFile(IReadOnlyList<CategoryRow> Categories, IReadOnlyList<ProductRow> Products);

    private sealed record CategoryRow(string Slug, string? ParentSlug, string Name, string? Description, int SortOrder, bool IsActive, string? ImageKey);

    private sealed record ProductRow(
        string Slug,
        string CategorySlug,
        string Name,
        IReadOnlyList<string>? LocalNames,
        string? SpeciesScientificName,
        string? Description,
        SourceType SourceType,
        string? SourceRegion,
        string? StorageInstructions,
        string? HandlingNotes,
        string? HsnCode,
        string? TaxCategoryCode,
        int? FeaturedRank,
        string? TargetStatus,
        IReadOnlyList<VariantRow> Variants);

    private sealed record VariantRow(
        string Sku,
        string Name,
        SellUnit SellUnit,
        int? PackSizeGrams,
        int? AvgPieceGrams,
        int? MinOrder,
        int? MaxOrder,
        int? OrderStep,
        bool? IsActive,
        IReadOnlyList<PreparationRow> Preparations,
        int? PiecesMin = null,
        int? PiecesMax = null,
        int? Serves = null);

    private sealed record PreparationRow(string Code, int YieldBp, bool IsDefault);

    private sealed record ImagesFile(IReadOnlyList<ProductPictures> Products, IReadOnlyList<CategoryPicture> Categories, IReadOnlyList<BannerPicture> Banners);

    private sealed record ProductPictures(string ProductSlug, ProductPlaceholder Placeholder, IReadOnlyList<ProductPicture>? Pictures);

    /// <summary>The drawing uploaded when the product has no committed picture, with its own alt text.</summary>
    private sealed record ProductPlaceholder(string AltText, int Width, int Height);

    /// <summary>Alt text of committed picture <c>images/products/{slug}/{Number}.webp</c>.</summary>
    private sealed record ProductPicture(int Number, string AltText);

    private sealed record CategoryPicture(string CategorySlug, string ImageKey, Placeholder Placeholder);

    private sealed record BannerPicture(string BannerKey, string ImageKey, Placeholder Placeholder);

    private sealed record Placeholder(int Width, int Height);
}
