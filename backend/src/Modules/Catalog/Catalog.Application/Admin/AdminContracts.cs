using FreshOrbit.Catalog.Domain.Products;

namespace FreshOrbit.Catalog.Application.Admin;

public sealed record CreateCategoryRequest(Guid? ParentId, string? Slug, string? Name, string? Description, int SortOrder);

public sealed record UpdateCategoryRequest(Guid? ParentId, string? Slug, string? Name, string? Description, string? ImageKey, int SortOrder, bool IsActive);

/// <param name="Version">Row version; send it back in <c>If-Match</c>.</param>
public sealed record AdminCategoryResponse(
    Guid Id,
    Guid? ParentId,
    string Slug,
    string Name,
    string? Description,
    string? ImageKey,
    int SortOrder,
    bool IsActive,
    string Version,
    IReadOnlyList<AdminCategoryResponse> Children);

/// <summary>Body of <c>POST</c> and <c>PUT /admin/products[/{id}]</c>.</summary>
public sealed record ProductRequest(
    Guid CategoryId,
    string? Slug,
    string? Name,
    IReadOnlyList<string>? LocalNames,
    string? SpeciesScientificName,
    string? Description,
    SourceType SourceType,
    string? SourceRegion,
    string? StorageInstructions,
    string? HandlingNotes,
    string? HsnCode,
    string? TaxCategoryCode,
    int? FeaturedRank);

/// <param name="Code">Preparation method code (WHOLE, STEAK, …).</param>
public sealed record VariantPreparationRequest(string? Code, int YieldBp, bool IsDefault);

/// <summary>
/// Body of <c>POST …/variants</c> (sku required) and <c>PUT …/variants/{variantId}</c> (sku ignored).
/// <c>minOrder</c>, <c>maxOrder</c> and <c>orderStep</c> are in the stock unit (grams for KG, else pieces/packs);
/// KG defaults 250 / 5000 / 250 (CAT-R7).
/// </summary>
public sealed record VariantRequest(
    string? Sku,
    string? Name,
    SellUnit SellUnit,
    int? PackSizeGrams,
    int? AvgPieceGrams,
    int? MinOrder,
    int? MaxOrder,
    int? OrderStep,
    bool? IsActive,
    IReadOnlyList<VariantPreparationRequest>? Preparations);

public sealed record ReorderImagesRequest(IReadOnlyList<Guid>? ImageIds);

public sealed record AdminPreparationResponse(string Code, string Name, int YieldBp, bool IsDefault, bool IsActive);

public sealed record AdminVariantResponse(
    Guid Id,
    string Sku,
    string Name,
    SellUnit SellUnit,
    StockUnit StockUnit,
    int? PackSizeGrams,
    int? AvgPieceGrams,
    int MinOrder,
    int MaxOrder,
    int OrderStep,
    bool IsWeightVariable,
    bool IsActive,
    IReadOnlyList<AdminPreparationResponse> Preparations);

/// <param name="Url">Public URL of the 640 px rendition.</param>
public sealed record AdminImageResponse(Guid Id, string StorageKey, string Url, string AltText, int Width, int Height, int SortOrder);

public sealed record AdminProductResponse(
    Guid Id,
    Guid CategoryId,
    string Slug,
    string Name,
    IReadOnlyList<string> LocalNames,
    string? SpeciesScientificName,
    string? Description,
    SourceType SourceType,
    string? SourceRegion,
    string? StorageInstructions,
    string? HandlingNotes,
    string? HsnCode,
    string? TaxCategoryCode,
    ProductStatus Status,
    int? FeaturedRank,
    DateTimeOffset? PublishedAt,
    string Version,
    IReadOnlyList<AdminVariantResponse> Variants,
    IReadOnlyList<AdminImageResponse> Images);

public sealed record AdminProductListItem(
    Guid Id,
    Guid CategoryId,
    string Slug,
    string Name,
    ProductStatus Status,
    int ActiveVariantCount,
    string? ImageUrl,
    DateTimeOffset? PublishedAt,
    string Version);

public sealed record PreparationMethodResponse(Guid Id, string Code, string Name, string? Description, int SortOrder, int GuidanceYieldBp);

/// <summary>Admin reads that need no aggregate (lists, trees).</summary>
public interface ICatalogAdminReads
{
    Task<IReadOnlyList<AdminCategoryResponse>> GetCategoryTreeAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<AdminCategoryResponse?> GetCategoryAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<AdminProductListItem> Items, int TotalCount)> ListProductsAsync(
        string? q,
        Guid? categoryId,
        IReadOnlyCollection<ProductStatus> statuses,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PreparationMethodResponse>> ListPreparationMethodsAsync(CancellationToken cancellationToken);
}
