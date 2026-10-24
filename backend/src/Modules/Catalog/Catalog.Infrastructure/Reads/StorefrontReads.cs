using System.Data.Common;
using System.Text;
using FreshOrbit.Catalog.Application.Admin;
using FreshOrbit.Catalog.Application.Storefront;
using FreshOrbit.Catalog.Domain.Categories;
using FreshOrbit.Catalog.Domain.Preparations;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Pricing;
using FreshOrbit.SharedKernel.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace FreshOrbit.Catalog.Infrastructure.Reads;

/// <summary>
/// Customer browse reads (CAT-R10 to CAT-R15). Prices and stock come only from the storefront views
/// (<c>catalog.storefront_offers</c>, <c>catalog.storefront_preparation_charges</c>, decision D3); periods are
/// filtered with the caller's instant so the application clock rules.
/// </summary>
public sealed class StorefrontReads(FreshOrbitDbContext db, IFileStorage storage) : IStorefrontReads
{
    public const double SimilarityThreshold = 0.3;
    public const int SuggestLimit = 8;
    public const string NetWeightNote = "You pay for net weight after cleaning. Final weight may vary up to 10% above the ordered weight.";

    private const string VisibleProductsFrom = """
        FROM catalog.products p
        JOIN catalog.categories c ON c.id = p.category_id AND c.is_active
        LEFT JOIN catalog.categories pc ON pc.id = c.parent_id
        WHERE p.status = 'ACTIVE' AND (c.parent_id IS NULL OR pc.is_active)
        """;

    private const string CurrentOffer = "o.valid_from <= @at AND (o.valid_to IS NULL OR o.valid_to > @at)";

    public async Task<StorefrontLocation?> ResolveLocationAsync(string pincode, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            "SELECT pincode, service_area_id, fulfillment_location_id, fulfillment_location_code, city_id FROM catalog.storefront_locations WHERE pincode = @pincode LIMIT 1",
            [new NpgsqlParameter("pincode", NpgsqlDbType.Text) { Value = pincode }],
            r => new StorefrontLocation(r.GetString(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3)) { CityId = r.GetGuid(4) },
            cancellationToken);
        return rows.FirstOrDefault();
    }

    public async Task<(IReadOnlyList<ProductListItem> Items, int TotalCount)> ListProductsAsync(ProductListQuery query, StorefrontLocation? location, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parameters = new List<NpgsqlParameter>
        {
            new("at", NpgsqlDbType.TimestampTz) { Value = at.UtcDateTime },
            new("skip", NpgsqlDbType.Integer) { Value = query.Skip },
            new("take", NpgsqlDbType.Integer) { Value = query.Take },
        };
        var sql = new StringBuilder("WITH base AS (SELECT p.id, p.slug, p.name, p.local_names, p.source_type, p.published_at, p.featured_rank, c.sort_order AS category_sort, p.category_id, ");
        sql.Append("(SELECT v.id FROM catalog.product_variants v WHERE v.product_id = p.id AND v.is_active AND EXISTS (SELECT 1 FROM catalog.variant_preparations x WHERE x.variant_id = v.id AND x.is_active) ORDER BY v.id LIMIT 1) AS first_variant_id, ");
        sql.Append(SearchRank(query.Q, parameters)).Append(" AS rank, ");
        sql.Append("(SELECT i.storage_key FROM catalog.product_images i WHERE i.product_id = p.id ORDER BY i.sort_order LIMIT 1) AS image_key ");
        sql.Append(VisibleProductsFrom);
        sql.Append(" AND EXISTS (SELECT 1 FROM catalog.product_variants v WHERE v.product_id = p.id AND v.is_active AND ")
            .Append(PreparationExists("v.id", query.PreparationCodes, parameters)).Append(')');
        if (query.CategorySlugs.Count > 0)
        {
            parameters.Add(new NpgsqlParameter("categories", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = query.CategorySlugs.ToArray() });
            sql.Append(" AND (c.slug = ANY(@categories) OR pc.slug = ANY(@categories))");
        }

        if (query.SourceTypes.Count > 0)
        {
            parameters.Add(new NpgsqlParameter("source_types", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = query.SourceTypes.Select(t => t.ToCode()).ToArray() });
            sql.Append(" AND p.source_type = ANY(@source_types)");
        }

        if (query.Q is not null)
        {
            sql.Append(" AND (").Append(SearchMatch()).Append(')');
        }

        if (query.FeaturedOnly)
        {
            sql.Append(" AND p.featured_rank IS NOT NULL");
        }

        if (query.ProductIds is { } productIds)
        {
            parameters.Add(new NpgsqlParameter("product_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = productIds.ToArray() });
            sql.Append(" AND p.id = ANY(@product_ids)");
        }

        sql.Append(')');
        if (location is null)
        {
            sql.Append(" SELECT b.id, b.slug, b.name, b.local_names, b.source_type, b.image_key, NULL::bigint, NULL::bigint, NULL::text, NULL::integer, NULL::boolean, count(*) OVER (), b.first_variant_id, b.category_id FROM base b");
            sql.Append(" ORDER BY ").Append(SortExpression(query.Sort)).Append(", b.id");
        }
        else
        {
            parameters.Add(new NpgsqlParameter("location", NpgsqlDbType.Uuid) { Value = location.FulfillmentLocationId });
            sql.Append(", offer_rows AS (SELECT o.product_id, o.variant_id, o.sell_unit, o.unit_price_paise, o.compare_at_paise, o.available, o.low_stock_threshold");
            sql.Append(" FROM catalog.storefront_offers o WHERE o.fulfillment_location_id = @location AND o.has_stock_item AND ").Append(CurrentOffer);
            if (query.PreparationCodes.Count > 0)
            {
                sql.Append(" AND ").Append(PreparationExists("o.variant_id", query.PreparationCodes, parameters));
            }

            sql.Append("), best AS (SELECT DISTINCT ON (product_id) product_id, variant_id, unit_price_paise, compare_at_paise, sell_unit FROM offer_rows ORDER BY product_id, unit_price_paise, variant_id)");
            sql.Append(", stock AS (SELECT product_id, max(available) AS available, bool_or(available > low_stock_threshold) AS above_threshold FROM offer_rows GROUP BY product_id)");
            sql.Append(" SELECT b.id, b.slug, b.name, b.local_names, b.source_type, b.image_key, best.unit_price_paise, best.compare_at_paise, best.sell_unit, s.available, s.above_threshold, count(*) OVER (), best.variant_id, b.category_id");
            sql.Append(" FROM base b JOIN best ON best.product_id = b.id JOIN stock s ON s.product_id = b.id WHERE true");
            if (query.InStock is { } inStock)
            {
                sql.Append(inStock ? " AND s.available > 0" : " AND s.available <= 0");
            }

            if (query.MinPricePaise is { } min)
            {
                parameters.Add(new NpgsqlParameter("min_price", NpgsqlDbType.Bigint) { Value = min });
                sql.Append(" AND best.unit_price_paise >= @min_price");
            }

            if (query.MaxPricePaise is { } max)
            {
                parameters.Add(new NpgsqlParameter("max_price", NpgsqlDbType.Bigint) { Value = max });
                sql.Append(" AND best.unit_price_paise <= @max_price");
            }

            sql.Append(" ORDER BY (CASE WHEN s.available > 0 THEN 0 ELSE 1 END), ").Append(SortExpression(query.Sort)).Append(", b.id");
        }

        sql.Append(" OFFSET @skip LIMIT @take");
        var total = 0;
        var items = await QueryAsync(sql.ToString(), parameters, r =>
        {
            total = (int)r.GetInt64(11);
            return (ToListItem(r, location is not null) with { CategoryId = r.GetGuid(13) }, r.IsDBNull(12) ? (Guid?)null : r.GetGuid(12));
        }, cancellationToken);
        if (items.Count == 0 && query.Skip > 0)
        {
            var (_, firstPageTotal) = await ListProductsAsync(query with { Skip = 0, Take = 1 }, location, at, cancellationToken);
            total = firstPageTotal;
        }

        return (await WithDefaultVariantsAsync(items, cancellationToken), total);
    }

    public async Task<IReadOnlyList<ProductListItem>> ListProductsByIdsAsync(IReadOnlyList<Guid> productIds, StorefrontLocation location, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        if (productIds.Count == 0)
        {
            return [];
        }

        var query = new ProductListQuery([], null, [], [], InStock: true, null, null, ProductSort.Default, 0, productIds.Count) { ProductIds = productIds };
        var (items, _) = await ListProductsAsync(query, location, at, cancellationToken);
        var rank = productIds.Select((id, index) => (id, index)).GroupBy(x => x.id).ToDictionary(g => g.Key, g => g.First().index);
        return [.. items.OrderBy(i => rank[i.Id])];
    }

    public async Task<IReadOnlyList<StorefrontPreparationMethod>> ListPreparationMethodsAsync(StorefrontLocation? location, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var parameters = new List<NpgsqlParameter>();
        var sql = new StringBuilder("SELECT m.code, m.name, m.description, m.sort_order FROM catalog.preparation_methods m WHERE EXISTS (SELECT 1 FROM catalog.variant_preparations x JOIN catalog.product_variants v ON v.id = x.variant_id AND v.is_active JOIN catalog.products p ON p.id = v.product_id JOIN catalog.categories c ON c.id = p.category_id AND c.is_active LEFT JOIN catalog.categories pc ON pc.id = c.parent_id WHERE x.preparation_method_id = m.id AND x.is_active AND p.status = 'ACTIVE' AND (c.parent_id IS NULL OR pc.is_active)");
        if (location is not null)
        {
            parameters.Add(new NpgsqlParameter("location", NpgsqlDbType.Uuid) { Value = location.FulfillmentLocationId });
            parameters.Add(new NpgsqlParameter("at", NpgsqlDbType.TimestampTz) { Value = at.UtcDateTime });
            sql.Append(" AND EXISTS (SELECT 1 FROM catalog.storefront_offers o WHERE o.variant_id = v.id AND o.fulfillment_location_id = @location AND o.has_stock_item AND ").Append(CurrentOffer).Append(')');
            sql.Append(" AND EXISTS (SELECT 1 FROM catalog.storefront_preparation_charges o WHERE o.variant_id = v.id AND o.preparation_method_id = m.id AND o.fulfillment_location_id = @location AND ").Append(CurrentOffer).Append(')');
        }

        sql.Append(") ORDER BY m.sort_order, m.code");
        return await QueryAsync(sql.ToString(), parameters, r => new StorefrontPreparationMethod(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetInt32(3)), cancellationToken);
    }

    /// <summary>CAT-R23: adds each item's default variant and its order rules (one query for the page).</summary>
    private async Task<IReadOnlyList<ProductListItem>> WithDefaultVariantsAsync(List<(ProductListItem Item, Guid? VariantId)> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Where(r => r.VariantId is not null).Select(r => r.VariantId!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [.. rows.Select(r => r.Item)];
        }

        var variants = await db.Set<ProductVariant>().AsNoTracking().Include(v => v.Preparations).Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, cancellationToken);
        var methods = await db.Set<PreparationMethod>().AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Code, cancellationToken);
        return [.. rows.Select(r => r.VariantId is { } id && variants.TryGetValue(id, out var v)
            ? r.Item with
            {
                DefaultVariant = new ListVariant(
                    v.Id,
                    v.Name,
                    v.SellUnit,
                    Quantity(v.MinOrder, v.SellUnit),
                    Quantity(v.MaxOrder, v.SellUnit),
                    Quantity(v.OrderStep, v.SellUnit),
                    v.DefaultPreparation is { } d ? methods.GetValueOrDefault(d.PreparationMethodId) : null,
                    ServingOf(v)),
            }
            : r.Item)];
    }

    private static ServingInfo ServingOf(ProductVariant v)
        => ServingInfo.From(ServingRules.ForBasis(v.SellUnit, v.PackSizeGrams, v.AvgPieceGrams, v.PiecesMin, v.PiecesMax, v.Serves));

    public async Task<StorefrontProductDetail?> GetProductAsync(string slug, StorefrontLocation? location, string? preparationCode, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var product = await db.Set<Product>().AsNoTracking()
            .Include(p => p.Variants).ThenInclude(v => v.Preparations)
            .Include(p => p.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Status == ProductStatus.Active, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var categories = await db.Set<Category>().AsNoTracking().Where(c => c.Id == product.CategoryId || db.Set<Category>().Any(x => x.Id == product.CategoryId && x.ParentId == c.Id)).ToListAsync(cancellationToken);
        var category = categories.FirstOrDefault(c => c.Id == product.CategoryId);
        var parent = categories.FirstOrDefault(c => c.Id == category?.ParentId);
        if (category is not { IsActive: true } || (category.ParentId is not null && parent is not { IsActive: true }))
        {
            return null;
        }

        var methods = await db.Set<PreparationMethod>().AsNoTracking().ToDictionaryAsync(m => m.Id, cancellationToken);
        var variants = product.Variants.Where(v => v.IsActive && v.Preparations.Any(p => p.IsActive)).ToList();
        var offers = location is null ? [] : await OffersAsync(location, [.. variants.Select(v => v.Id)], at, cancellationToken);
        var charges = location is null ? [] : await ChargesAsync(location, [.. variants.Select(v => v.Id)], at, cancellationToken);

        var variantResponses = variants.Select(v =>
        {
            offers.TryGetValue(v.Id, out var offer);
            var preparations = v.Preparations.Where(p => p.IsActive)
                .Select(p => (Preparation: p, Method: methods[p.PreparationMethodId]))
                .OrderBy(x => x.Method.SortOrder)
                .Select(x =>
                {
                    charges.TryGetValue((v.Id, x.Preparation.PreparationMethodId), out var charge);
                    return new StorefrontPreparation(x.Method.Code, x.Method.Name, x.Preparation.YieldBp, x.Preparation.IsDefault, charge?.ChargePaise, charge?.ChargeBasis);
                })
                .ToList();
            var yieldBp = ChosenYield(v, methods, preparationCode);
            return new StorefrontVariant(
                v.Id,
                v.Sku,
                v.Name,
                v.SellUnit,
                v.IsWeightVariable,
                v.PackSizeGrams,
                v.AvgPieceGrams,
                Quantity(v.MinOrder, v.SellUnit),
                Quantity(v.MaxOrder, v.SellUnit),
                Quantity(v.OrderStep, v.SellUnit),
                offer?.UnitPricePaise,
                offer?.CompareAtPaise,
                preparations,
                location is null ? null : VariantAvailabilityFor(v, offer, yieldBp))
            {
                DiscountBp = offer is null ? null : CompareAtDiscount.Bp(offer.UnitPricePaise, offer.CompareAtPaise),
                Serving = ServingOf(v),
            };
        }).ToList();

        ListAvailability? availability = null;
        if (location is not null)
        {
            var statuses = variantResponses.Select(v => v.Availability!.Status).ToList();
            var status = statuses.Contains(AvailabilityStatus.InStock) ? AvailabilityStatus.InStock
                : statuses.Contains(AvailabilityStatus.LowStock) ? AvailabilityStatus.LowStock
                : statuses.Contains(AvailabilityStatus.OutOfStock) ? AvailabilityStatus.OutOfStock
                : AvailabilityStatus.NotSoldHere;
            availability = new ListAvailability(status, StockMath.Message(status));
        }

        return new StorefrontProductDetail(
            product.Id,
            product.Slug,
            product.Name,
            product.LocalNames,
            product.SpeciesScientificName,
            product.Description,
            product.SourceType,
            product.SourceRegion,
            product.StorageInstructions,
            new CategoryRef(category.Slug, category.Name),
            [.. product.Images.OrderBy(i => i.SortOrder).Select(ToImage)],
            location is null ? null : new LocationRef(location.FulfillmentLocationCode, location.Pincode),
            variantResponses,
            availability,
            NetWeightNote,
            null,
            location is null)
        {
            CategoryId = product.CategoryId,
        };
    }

    public async Task<IReadOnlyList<SuggestItem>> SuggestAsync(string q, StorefrontLocation? location, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var parameters = new List<NpgsqlParameter> { new("at", NpgsqlDbType.TimestampTz) { Value = at.UtcDateTime } };
        var rank = SearchRank(q, parameters);
        var sql = new StringBuilder("SELECT p.name, p.slug, ");
        sql.Append("(SELECT n FROM unnest(p.local_names) AS n WHERE word_similarity(@q, n) >= @threshold ORDER BY word_similarity(@q, n) DESC LIMIT 1), ");
        sql.Append("(SELECT i.storage_key FROM catalog.product_images i WHERE i.product_id = p.id ORDER BY i.sort_order LIMIT 1), ");
        sql.Append(rank).Append(" AS rank ");
        sql.Append(VisibleProductsFrom);
        sql.Append(" AND EXISTS (SELECT 1 FROM catalog.product_variants v WHERE v.product_id = p.id AND v.is_active AND ").Append(PreparationExists("v.id", [], parameters)).Append(')');
        sql.Append(" AND (").Append(SearchMatch()).Append(')');
        if (location is not null)
        {
            parameters.Add(new NpgsqlParameter("location", NpgsqlDbType.Uuid) { Value = location.FulfillmentLocationId });
            sql.Append(" AND EXISTS (SELECT 1 FROM catalog.storefront_offers o WHERE o.product_id = p.id AND o.fulfillment_location_id = @location AND o.has_stock_item AND ").Append(CurrentOffer).Append(')');
        }

        parameters.Add(new NpgsqlParameter("limit", NpgsqlDbType.Integer) { Value = SuggestLimit });
        sql.Append(" ORDER BY rank DESC, p.name, p.id LIMIT @limit");
        return await QueryAsync(sql.ToString(), parameters, r => new SuggestItem(
            r.GetString(0),
            r.IsDBNull(2) ? null : r.GetString(2),
            r.GetString(1),
            r.IsDBNull(3) ? null : ImageUrl(r.GetString(3), ImageRenditions.Small)), cancellationToken);
    }

    public async Task<IReadOnlyList<StorefrontCategory>> GetCategoryTreeAsync(CancellationToken cancellationToken)
    {
        var all = await db.Set<Category>().AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(cancellationToken);
        return [.. all.Where(c => c.ParentId is null).Select(c => ToCategory(c, [.. all.Where(x => x.ParentId == c.Id).Select(x => ToCategory(x, []))]))];
    }

    public async Task<IReadOnlyList<AvailabilityItem>> GetAvailabilityAsync(StorefrontLocation location, IReadOnlyCollection<Guid> variantIds, string? preparationCode, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        var ids = variantIds.Distinct().ToList();
        var variants = await db.Set<ProductVariant>().AsNoTracking().Include(v => v.Preparations).Where(v => ids.Contains(v.Id)).ToListAsync(cancellationToken);
        var methods = await db.Set<PreparationMethod>().AsNoTracking().ToDictionaryAsync(m => m.Id, cancellationToken);
        var offers = await OffersAsync(location, ids, at, cancellationToken);
        return [.. ids.Select(id =>
        {
            var variant = variants.FirstOrDefault(v => v.Id == id);
            if (variant is null || !offers.TryGetValue(id, out var offer))
            {
                return new AvailabilityItem(id, AvailabilityStatus.NotSoldHere, preparationCode, null, null, StockMath.Message(AvailabilityStatus.NotSoldHere));
            }

            var yieldBp = ChosenYield(variant, methods, preparationCode);
            var chosenCode = methods.Values.FirstOrDefault(m => m.Code == preparationCode && variant.Preparations.Any(p => p.IsActive && p.PreparationMethodId == m.Id))?.Code
                ?? (variant.DefaultPreparation is { } d ? methods[d.PreparationMethodId].Code : null);
            var availability = VariantAvailabilityFor(variant, offer, yieldBp);
            return new AvailabilityItem(id, availability.Status, chosenCode, availability.MaxOrderableNow, offer.UnitPricePaise, availability.Message);
        })];
    }

    private static int ChosenYield(ProductVariant variant, Dictionary<Guid, PreparationMethod> methods, string? preparationCode)
    {
        var chosen = variant.Preparations.FirstOrDefault(p => p.IsActive && methods.TryGetValue(p.PreparationMethodId, out var m) && m.Code == preparationCode)
            ?? variant.Preparations.FirstOrDefault(p => p.IsActive && p.IsDefault)
            ?? variant.Preparations.FirstOrDefault(p => p.IsActive);
        return chosen?.YieldBp ?? SharedKernel.Units.BasisPoints.Whole;
    }

    private static VariantAvailability VariantAvailabilityFor(ProductVariant variant, OfferRow? offer, int yieldBp)
    {
        if (offer is null)
        {
            return new VariantAvailability(AvailabilityStatus.NotSoldHere, null, StockMath.Message(AvailabilityStatus.NotSoldHere));
        }

        var maxOrderable = StockMath.MaxOrderable(offer.Available, variant.SellUnit, yieldBp, variant.MinOrder, variant.MaxOrder, variant.OrderStep);
        var status = maxOrderable == 0 ? AvailabilityStatus.OutOfStock : StockMath.Status(offer.Available, offer.LowStockThreshold);
        return new VariantAvailability(status, Quantity(maxOrderable, variant.SellUnit), StockMath.Message(status));
    }

    private async Task<Dictionary<Guid, OfferRow>> OffersAsync(StorefrontLocation location, List<Guid> variantIds, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (variantIds.Count == 0)
        {
            return [];
        }

        var rows = await QueryAsync(
            $"SELECT o.variant_id, o.unit_price_paise, o.compare_at_paise, o.available, o.low_stock_threshold FROM catalog.storefront_offers o WHERE o.fulfillment_location_id = @location AND o.variant_id = ANY(@ids) AND o.has_stock_item AND {CurrentOffer}",
            [
                new NpgsqlParameter("location", NpgsqlDbType.Uuid) { Value = location.FulfillmentLocationId },
                new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = variantIds.ToArray() },
                new NpgsqlParameter("at", NpgsqlDbType.TimestampTz) { Value = at.UtcDateTime },
            ],
            r => new OfferRow(r.GetGuid(0), r.GetInt64(1), r.IsDBNull(2) ? null : r.GetInt64(2), r.GetInt32(3), r.GetInt32(4)),
            cancellationToken);
        return rows.ToDictionary(r => r.VariantId);
    }

    private async Task<Dictionary<(Guid VariantId, Guid MethodId), ChargeRow>> ChargesAsync(StorefrontLocation location, List<Guid> variantIds, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (variantIds.Count == 0)
        {
            return [];
        }

        var rows = await QueryAsync(
            $"SELECT o.variant_id, o.preparation_method_id, o.charge_paise, o.charge_basis FROM catalog.storefront_preparation_charges o WHERE o.fulfillment_location_id = @location AND o.variant_id = ANY(@ids) AND {CurrentOffer}",
            [
                new NpgsqlParameter("location", NpgsqlDbType.Uuid) { Value = location.FulfillmentLocationId },
                new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = variantIds.ToArray() },
                new NpgsqlParameter("at", NpgsqlDbType.TimestampTz) { Value = at.UtcDateTime },
            ],
            r => new ChargeRow(r.GetGuid(0), r.GetGuid(1), r.GetInt64(2), r.GetString(3)),
            cancellationToken);
        return rows.ToDictionary(r => (r.VariantId, r.MethodId));
    }

    /// <summary>The rank expression; adds <c>@q</c>, <c>@tsquery</c> and <c>@threshold</c> when searching (CAT-R12).</summary>
    private static string SearchRank(string? q, List<NpgsqlParameter> parameters)
    {
        if (q is null)
        {
            return "0::double precision";
        }

        var tokens = StorefrontService.Tokens(q);
        var tsquery = string.Join(" & ", tokens.Select((t, i) => i == tokens.Count - 1 ? $"{t}:*" : t));
        parameters.Add(new NpgsqlParameter("q", NpgsqlDbType.Text) { Value = q });
        parameters.Add(new NpgsqlParameter("tsquery", NpgsqlDbType.Text) { Value = tsquery });
        parameters.Add(new NpgsqlParameter("threshold", NpgsqlDbType.Double) { Value = SimilarityThreshold });
        return "(ts_rank_cd(p.search_vector, to_tsquery('simple', @tsquery)) * 2 + greatest(word_similarity(@q, p.name), word_similarity(@q, catalog.immutable_array_to_string(p.local_names, ' '))))";
    }

    private static string SearchMatch()
        => "p.search_vector @@ to_tsquery('simple', @tsquery) OR word_similarity(@q, p.name) >= @threshold OR word_similarity(@q, catalog.immutable_array_to_string(p.local_names, ' ')) >= @threshold";

    private static string PreparationExists(string variantColumn, IReadOnlyList<string> codes, List<NpgsqlParameter> parameters)
    {
        if (codes.Count == 0)
        {
            return $"EXISTS (SELECT 1 FROM catalog.variant_preparations x WHERE x.variant_id = {variantColumn} AND x.is_active)";
        }

        if (!parameters.Any(p => p.ParameterName == "preparations"))
        {
            parameters.Add(new NpgsqlParameter("preparations", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = codes.ToArray() });
        }

        return $"EXISTS (SELECT 1 FROM catalog.variant_preparations x JOIN catalog.preparation_methods m ON m.id = x.preparation_method_id WHERE x.variant_id = {variantColumn} AND x.is_active AND m.code = ANY(@preparations))";
    }

    private static string SortExpression(ProductSort sort) => sort switch
    {
        ProductSort.Relevance => "b.rank DESC",
        ProductSort.Price => "best.unit_price_paise",
        ProductSort.PriceDescending => "best.unit_price_paise DESC",
        ProductSort.Name => "b.name",
        ProductSort.Newest => "b.published_at DESC NULLS LAST",
        ProductSort.Featured => "b.featured_rank, b.name",
        _ => "b.category_sort, b.name",
    };

    private ProductListItem ToListItem(DbDataReader r, bool hasLocation)
    {
        FromPrice? fromPrice = null;
        ListAvailability? availability = null;
        if (hasLocation)
        {
            var price = r.GetInt64(6);
            long? compareAt = r.IsDBNull(7) ? null : r.GetInt64(7);
            fromPrice = new FromPrice(price, compareAt, r.GetString(8).ToEnum<SellUnit>()) { DiscountBp = CompareAtDiscount.Bp(price, compareAt) };
            var available = r.GetInt32(9);
            var status = available <= 0 ? AvailabilityStatus.OutOfStock : r.GetBoolean(10) ? AvailabilityStatus.InStock : AvailabilityStatus.LowStock;
            availability = new ListAvailability(status, StockMath.Message(status));
        }

        return new ProductListItem(
            r.GetGuid(0),
            r.GetString(1),
            r.GetString(2),
            r.GetFieldValue<string[]>(3),
            r.GetString(4).ToEnum<SourceType>(),
            r.IsDBNull(5) ? null : ImageUrl(r.GetString(5), ImageRenditions.Medium),
            fromPrice,
            availability,
            !hasLocation);
    }

    private StorefrontImage ToImage(ProductImage image) => new(
        ImageUrl(image.StorageKey, ImageRenditions.Large),
        image.AltText,
        image.Width,
        image.Height,
        [.. ImageRenditions.Widths.Select(w => new ImageSource(w, ImageUrl(image.StorageKey, w)))]);

    private StorefrontCategory ToCategory(Category category, IReadOnlyList<StorefrontCategory> children) => new(
        category.Id,
        category.Slug,
        category.Name,
        category.Description,
        category.ImageKey is { } key && StorageKeys.IsValid(key) ? storage.GetPublicUrl(key) : null,
        category.SortOrder,
        children);

    private string ImageUrl(string prefix, int width) => storage.GetPublicUrl(ImageRenditions.Key(prefix, width));

    private static QuantityValue Quantity(int value, SellUnit sellUnit) => new(value, QuantityRules.UnitFor(sellUnit));

    private async Task<List<T>> QueryAsync<T>(string sql, IReadOnlyList<NpgsqlParameter> parameters, Func<DbDataReader, T> map, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            var result = new List<T>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(map(reader));
            }

            return result;
        }
        finally
        {
            if (opened)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }

    private sealed record OfferRow(Guid VariantId, long UnitPricePaise, long? CompareAtPaise, int Available, int LowStockThreshold);

    private sealed record ChargeRow(Guid VariantId, Guid MethodId, long ChargePaise, string ChargeBasis);
}
