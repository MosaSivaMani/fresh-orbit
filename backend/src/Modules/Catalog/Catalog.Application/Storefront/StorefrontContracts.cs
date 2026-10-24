using FreshOrbit.Catalog.Domain.Products;

namespace FreshOrbit.Catalog.Application.Storefront;

/// <summary>The resolved location context (docs/api §1.4): pincode → active service area → active location in an active city.</summary>
public sealed record StorefrontLocation(string Pincode, Guid ServiceAreaId, Guid FulfillmentLocationId, string FulfillmentLocationCode)
{
    /// <summary>City of the fulfillment location (city-scoped offers).</summary>
    public Guid? CityId { get; init; }
}

/// <summary>A quantity with its unit: GRAM, PIECE or PACK.</summary>
public sealed record QuantityValue(int Value, QuantityUnit Unit);

/// <param name="PerUnit">KG, PIECE or PACK (the variant's sell unit).</param>
public sealed record FromPrice(long UnitPricePaise, long? CompareAtPaise, SellUnit PerUnit)
{
    /// <summary>Saving against <see cref="CompareAtPaise"/> in basis points (floor); null unless compare-at is above the price (CAT-R22).</summary>
    public int? DiscountBp { get; init; }
}

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

    /// <summary>Only these products (bestsellers); null for no id filter.</summary>
    public IReadOnlyList<Guid>? ProductIds { get; init; }
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
    bool LocationRequired)
{
    /// <summary>
    /// The variant quick-add uses (CAT-R23): with a location the variant behind <see cref="FromPrice"/>, else the first
    /// visible variant. Null only when the product has no visible variant.
    /// </summary>
    public ListVariant? DefaultVariant { get; init; }

    /// <summary>Freshness of <see cref="DefaultVariant"/> at the location (CAT-R25); null without location or when unknown.</summary>
    public FreshnessInfo? Freshness { get; init; }

    /// <summary>A listed coupon that applies to the product in the location's city (CAT-R26); null when none or without location.</summary>
    public ProductOffer? Offer { get; init; }

    /// <summary>The category id (offer lookup); not serialized.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid CategoryId { get; init; }
}

/// <summary>A product's default variant with its order rules, so a card can add to cart without the detail call.</summary>
/// <param name="DefaultPreparationCode">The preparation the cart uses when none is sent.</param>
public sealed record ListVariant(
    Guid Id,
    string Name,
    SellUnit SellUnit,
    QuantityValue MinOrder,
    QuantityValue MaxOrder,
    QuantityValue OrderStep,
    string? DefaultPreparationCode,
    ServingInfo Serving);

/// <summary>What a quantity of a variant contains (CAT-R21), computed by the server.</summary>
/// <param name="Basis">The quantity described: 1000 GRAM for KG variants, 1 PACK, 1 PIECE (or the quoted quantity).</param>
/// <param name="NetWeightGrams">Net weight of <paramref name="Basis"/>; null for a PIECE variant without an average piece weight.</param>
/// <param name="PiecesMin">Fewest pieces in <paramref name="Basis"/>; null when not stated (always null for PIECE variants).</param>
/// <param name="Serves">People served by <paramref name="Basis"/>; null when not stated.</param>
public sealed record ServingInfo(QuantityValue Basis, int? NetWeightGrams, int? PiecesMin, int? PiecesMax, int? Serves)
{
    public static ServingInfo From(ServingFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return new(new QuantityValue(facts.BasisQuantity, facts.BasisUnit), facts.NetWeightGrams, facts.PiecesMin, facts.PiecesMax, facts.Serves);
    }
}

/// <summary>Freshness of the batch the next order is packed from (CAT-R25).</summary>
/// <param name="CaughtOn">Catch or harvest date recorded at receipt; null when not recorded.</param>
public sealed record FreshnessInfo(DateOnly? CaughtOn, DateTimeOffset ReceivedAt);

/// <summary>A coupon to show on a product (CAT-R26). The discount is computed only in the cart.</summary>
/// <param name="Text">Display sentence composed by the server.</param>
public sealed record ProductOffer(string CouponCode, string Text);

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
    VariantAvailability? Availability)
{
    /// <summary>Saving against <see cref="CompareAtPaise"/> in basis points (floor); null unless compare-at is above the price (CAT-R22).</summary>
    public int? DiscountBp { get; init; }

    /// <summary>Pieces, serves and net weight per serving basis (CAT-R21).</summary>
    public ServingInfo? Serving { get; init; }

    /// <summary>Freshness at the location (CAT-R25); null without location or when unknown.</summary>
    public FreshnessInfo? Freshness { get; init; }
}

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
    bool LocationRequired)
{
    /// <summary>A listed coupon that applies to the product in the location's city (CAT-R26); null when none or without location.</summary>
    public ProductOffer? Offer { get; init; }

    /// <summary>The category id (offer lookup); not serialized.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid CategoryId { get; init; }
}

public sealed record SuggestItem(string Name, string? MatchedLocalName, string Slug, string? ImageUrl);

public sealed record StorefrontCategory(Guid Id, string Slug, string Name, string? Description, string? ImageUrl, int SortOrder, IReadOnlyList<StorefrontCategory> Children);

public sealed record StorefrontCategoryDetail(Guid Id, string Slug, string Name, string? Description, string? ImageUrl, CategoryRef? Parent, IReadOnlyList<StorefrontCategory> Children);

/// <param name="Banners">Home banners belong to Promotions; empty until that module serves them.</param>
public sealed record HomeResponse(IReadOnlyList<object> Banners, IReadOnlyList<StorefrontCategory> FeaturedCategories, IReadOnlyList<ProductListItem> FeaturedProducts, bool LocationRequired)
{
    /// <summary>Best-selling products at the location from real orders (CAT-R24); empty without location or sales.</summary>
    public IReadOnlyList<ProductListItem> Bestsellers { get; init; } = [];
}

/// <summary><c>GET /bestsellers</c> (CAT-R24).</summary>
public sealed record BestsellersResponse(LocationRef Location, IReadOnlyList<ProductListItem> Items);

/// <summary>A preparation method offered on at least one visible variant (<c>GET /preparation-methods</c>, CAT-R27).</summary>
public sealed record StorefrontPreparationMethod(string Code, string Name, string? Description, int SortOrder);

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

    /// <summary>The given products that are visible and priced with a stock item at the location, in the given order.</summary>
    Task<IReadOnlyList<ProductListItem>> ListProductsByIdsAsync(IReadOnlyList<Guid> productIds, StorefrontLocation location, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Preparation methods offered on a visible variant (at the location when given, CAT-R27), by sort order.</summary>
    Task<IReadOnlyList<StorefrontPreparationMethod>> ListPreparationMethodsAsync(StorefrontLocation? location, DateTimeOffset at, CancellationToken cancellationToken);
}
