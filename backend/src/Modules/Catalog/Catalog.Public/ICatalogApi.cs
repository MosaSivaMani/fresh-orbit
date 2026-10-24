namespace FreshOrbit.Catalog.Public;

/// <summary>What other modules may ask the Catalog module.</summary>
public interface ICatalogApi
{
    // Members added in Phase 2 have default bodies so test doubles written against the Phase 1 contract still compile;
    // the module's real implementation overrides every one of them.
    private const string NewMemberNote = "Not implemented by this test double.";

    /// <summary>The requested variants that exist, active or not (missing ids are simply absent).</summary>
    Task<IReadOnlyList<VariantInfo>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default);

    /// <summary>The variants with the given SKUs that exist, active or not (missing SKUs are simply absent).</summary>
    Task<IReadOnlyList<VariantInfo>> GetVariantsBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>Ids of active variants of ACTIVE products (that have at least one active preparation).</summary>
    Task<IReadOnlyList<Guid>> GetSellableVariantIdsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>Name, slug, primary image and status of the requested products (missing ids are absent).</summary>
    Task<IReadOnlyList<ProductSummary>> GetProductSummariesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>
    /// For each existing category id (active or not): the category itself followed by its ancestors, nearest first.
    /// Missing ids are absent.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetCategoryLineagesAsync(IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>Category ids by slug for the slugs that exist, active or not (missing slugs are absent).</summary>
    Task<IReadOnlyDictionary<string, Guid>> GetCategoryIdsBySlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>Product ids by slug for the slugs that exist, any status (missing slugs are absent).</summary>
    Task<IReadOnlyDictionary<string, Guid>> GetProductIdsBySlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);

    /// <summary>
    /// Storefront cards of the requested products (missing ids are absent), priced at
    /// <paramref name="fulfillmentLocationId"/> from the same storefront offers the product list uses (one query each
    /// for products and offers). Without a location, or when not sold there, the price is null.
    /// </summary>
    Task<IReadOnlyList<ProductCard>> GetProductCardsAsync(IReadOnlyCollection<Guid> productIds, Guid? fulfillmentLocationId, DateTimeOffset at, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NewMemberNote);
}

/// <summary>A product as a storefront card (W5).</summary>
/// <param name="IsVisible">False when the product is not ACTIVE or its category is inactive (not shown on the storefront).</param>
/// <param name="ImageUrl">Public URL of the primary image (small rendition); null without an image.</param>
/// <param name="Price">The lowest current price of a stocked variant at the location; null without a location or when not sold there.</param>
/// <param name="IsAvailable">True when the price is known and some stocked variant has stock available now.</param>
public sealed record ProductCard(Guid ProductId, string Slug, string Name, string? ImageUrl, bool IsVisible, ProductCardPrice? Price, bool IsAvailable);

/// <param name="PerUnit">The variant's sell unit (KG, PIECE or PACK).</param>
public sealed record ProductCardPrice(long UnitPricePaise, long? CompareAtPaise, SellUnit PerUnit);

/// <summary>How a variant is sold. Stored as KG / PIECE / PACK.</summary>
public enum SellUnit
{
    Kg,
    Piece,
    Pack,
}

/// <summary>How a variant's stock is counted. Stored as GRAM / PIECE.</summary>
public enum StockUnit
{
    Gram,
    Piece,
}

/// <summary>Stored as DRAFT / ACTIVE / ARCHIVED.</summary>
public enum ProductStatus
{
    Draft,
    Active,
    Archived,
}

/// <param name="MinOrder">Minimum quantity in <paramref name="StockUnit"/> (grams or pieces); likewise MaxOrder and OrderStep.</param>
/// <param name="HsnCode">Empty while a DRAFT product has none (publishing requires it).</param>
/// <param name="TaxCategoryCode">Empty while a DRAFT product has none (publishing requires it).</param>
public sealed record VariantInfo(
    Guid VariantId,
    Guid ProductId,
    Guid CategoryId,
    string ProductName,
    string ProductSlug,
    string VariantName,
    string Sku,
    string? ImageKey,
    string HsnCode,
    string TaxCategoryCode,
    SellUnit SellUnit,
    StockUnit StockUnit,
    int? PackSizeGrams,
    int? AvgPieceGrams,
    int MinOrder,
    int MaxOrder,
    int OrderStep,
    bool IsWeightVariable,
    bool IsActive,
    IReadOnlyList<PreparationInfo> Preparations)
{
    public ProductStatus ProductStatus { get; init; }
}

/// <param name="YieldBp">Net weight after preparation as basis points of raw weight (6500 = 65 %).</param>
public sealed record PreparationInfo(Guid PreparationMethodId, string Code, string Name, int YieldBp, bool IsDefault, bool IsActive);

/// <param name="ImageKey">Storage key prefix of the primary image (<c>products/{productId}/{imageId}</c>); null without images.</param>
public sealed record ProductSummary(Guid ProductId, string Name, string Slug, string? ImageKey, ProductStatus Status);
