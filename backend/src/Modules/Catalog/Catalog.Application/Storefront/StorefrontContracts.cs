using FreshOrbit.Catalog.Domain.Products;

namespace FreshOrbit.Catalog.Application.Storefront;

/// <summary>The resolved location context (docs/api §1.4): pincode → active service area → active location in an active city.</summary>
public sealed record StorefrontLocation(string Pincode, Guid ServiceAreaId, Guid FulfillmentLocationId, string FulfillmentLocationCode);

/// <summary>A quantity with its unit: GRAM, PIECE or PACK.</summary>
public sealed record QuantityValue(int Value, QuantityUnit Unit);

/// <param name="PerUnit">KG, PIECE or PACK (the variant's sell unit).</param>
public sealed record FromPrice(long UnitPricePaise, long? CompareAtPaise, SellUnit PerUnit);

/// <param name="Status">IN_STOCK, LOW_STOCK, OUT_OF_STOCK or NOT_SOLD_HERE.</param>
public sealed record ListAvailability(AvailabilityStatus Status, string? Message);

public enum AvailabilityStatus
{
    InStock,
    LowStock,
    OutOfStock,
    NotSoldHere,
}

public enum ProductSort
{
    Default,
    Relevance,
    Price,
    PriceDescending,
    Name,
    Newest,

    /// <summary>Home page: products with a featured rank, by rank.</summary>
    Featured,
}

/// <summary>Validated filters of <c>GET /products</c> (CAT-R14, CAT-R15).</summary>
public sealed record ProductListQuery(
    IReadOnlyList<string> CategorySlugs,
    string? Q,
    IReadOnlyList<SourceType> SourceTypes,
    IReadOnlyList<string> PreparationCodes,
    bool? InStock,
    long? MinPricePaise,
    long? MaxPricePaise,
    ProductSort Sort,
    int Skip,
    int Take)
{
    /// <summary>Only products with a featured rank (home page).</summary>
    public bool FeaturedOnly { get; init; }
}

public sealed record ProductListItem(
    Guid Id,
    string Slug,
    string Name,
    IReadOnlyList<string> LocalNames,
    SourceType SourceType,
    string? ImageUrl,
    FromPrice? FromPrice,
    ListAvailability? Availability,
    bool LocationRequired);

public sealed record CategoryRef(string Slug, string Name);

public sealed record StorefrontImage(string Url, string AltText, int Width, int Height, IReadOnlyList<ImageSource> Sources);

public sealed record ImageSource(int Width, string Url);

public sealed record LocationRef(string FulfillmentLocationCode, string Pincode);

/// <param name="ChargePaise">Null when the location has no charge row (the preparation cannot be ordered there, PRC-R8).</param>
/// <param name="ChargeBasis">PER_KG, PER_ITEM or FLAT; null like <paramref name="ChargePaise"/>.</param>
public sealed record StorefrontPreparation(string Code, string Name, int YieldBp, bool IsDefault, long? ChargePaise, string? ChargeBasis);

/// <param name="MaxOrderableNow">Largest valid quantity the stock covers for the chosen (or default) preparation.</param>
public sealed record VariantAvailability(AvailabilityStatus Status, QuantityValue? MaxOrderableNow, string? Message);

public sealed record StorefrontVariant(
    Guid Id,
    string Sku,
    string Name,
    SellUnit SellUnit,
    bool IsWeightVariable,
    int? PackSizeGrams,
    int? AvgPieceGrams,
    QuantityValue MinOrder,
    QuantityValue MaxOrder,
    QuantityValue OrderStep,
    long? UnitPricePaise,
    long? CompareAtPaise,
    IReadOnlyList<StorefrontPreparation> Preparations,
    VariantAvailability? Availability);

/// <param name="IsWishlisted">Null: wishlists belong to Customers, which Catalog does not call.</param>
public sealed record StorefrontProductDetail(
    Guid Id,
    string Slug,
    string Name,
    IReadOnlyList<string> LocalNames,
    string? SpeciesScientificName,
    string? Description,
    SourceType SourceType,
    string? SourceRegion,
    string? StorageInstructions,
    CategoryRef Category,
    IReadOnlyList<StorefrontImage> Images,
    LocationRef? Location,
    IReadOnlyList<StorefrontVariant> Variants,
    ListAvailability? Availability,
    string NetWeightNote,
    bool? IsWishlisted,
    bool LocationRequired);

public sealed record SuggestItem(string Name, string? MatchedLocalName, string Slug, string? ImageUrl);

public sealed record StorefrontCategory(Guid Id, string Slug, string Name, string? Description, string? ImageUrl, int SortOrder, IReadOnlyList<StorefrontCategory> Children);

public sealed record StorefrontCategoryDetail(Guid Id, string Slug, string Name, string? Description, string? ImageUrl, CategoryRef? Parent, IReadOnlyList<StorefrontCategory> Children);

/// <param name="Banners">Home banners belong to Promotions; empty until that module serves them.</param>
public sealed record HomeResponse(IReadOnlyList<object> Banners, IReadOnlyList<StorefrontCategory> FeaturedCategories, IReadOnlyList<ProductListItem> FeaturedProducts, bool LocationRequired);

public sealed record AvailabilityItem(Guid VariantId, AvailabilityStatus Status, string? PreparationCode, QuantityValue? MaxOrderableNow, long? UnitPricePaise, string? Message);

public sealed record AvailabilityResponse(LocationRef Location, IReadOnlyList<AvailabilityItem> Items);

/// <summary>Customer browse reads over <c>catalog.storefront_offers</c> (CAT-R10 to CAT-R15).</summary>
public interface IStorefrontReads
{
    Task<StorefrontLocation?> ResolveLocationAsync(string pincode, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ProductListItem> Items, int TotalCount)> ListProductsAsync(ProductListQuery query, StorefrontLocation? location, DateTimeOffset at, CancellationToken cancellationToken);

    Task<StorefrontProductDetail?> GetProductAsync(string slug, StorefrontLocation? location, string? preparationCode, DateTimeOffset at, CancellationToken cancellationToken);

    Task<IReadOnlyList<SuggestItem>> SuggestAsync(string q, StorefrontLocation? location, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Active categories whose parent is active, as a tree.</summary>
    Task<IReadOnlyList<StorefrontCategory>> GetCategoryTreeAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AvailabilityItem>> GetAvailabilityAsync(StorefrontLocation location, IReadOnlyCollection<Guid> variantIds, string? preparationCode, DateTimeOffset at, CancellationToken cancellationToken);
}
