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
}

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
