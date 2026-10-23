using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Catalog.Infrastructure.Persistence;

/// <summary>The Catalog module's tables, all in the <c>catalog</c> schema, plus its search column and storefront views.</summary>
public sealed class CatalogModuleModel : IModuleModel
{
    public const string SchemaName = "catalog";

    /// <summary>IMMUTABLE wrapper so <c>local_names</c> can feed a generated column and an expression index (catalog.md Q3).</summary>
    public const string ArrayToStringFunctionSql = """
        CREATE OR REPLACE FUNCTION catalog.immutable_array_to_string(text[], text)
        RETURNS text LANGUAGE sql IMMUTABLE PARALLEL SAFE
        AS $$ SELECT array_to_string($1, $2) $$;
        """;

    /// <summary>CAT-R12: weighted <c>simple</c> tsvector plus trigram indexes on name and local names.</summary>
    public const string SearchSql = """
        ALTER TABLE catalog.products ADD COLUMN IF NOT EXISTS search_vector tsvector GENERATED ALWAYS AS (
            setweight(to_tsvector('simple'::regconfig, coalesce(name, '')), 'A') ||
            setweight(to_tsvector('simple'::regconfig, coalesce(catalog.immutable_array_to_string(local_names, ' '), '')), 'A') ||
            setweight(to_tsvector('simple'::regconfig, coalesce(species_scientific_name, '')), 'B') ||
            setweight(to_tsvector('simple'::regconfig, coalesce(description, '')), 'C')) STORED;
        CREATE INDEX IF NOT EXISTS ix_products_search_vector ON catalog.products USING gin (search_vector);
        CREATE INDEX IF NOT EXISTS ix_products_name_trgm ON catalog.products USING gin (name gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_products_local_names_trgm ON catalog.products
            USING gin (catalog.immutable_array_to_string(local_names, ' ') gin_trgm_ops);
        """;

    /// <summary>CAT-R5: at most one active default preparation per variant, checked at commit.</summary>
    public const string DefaultPreparationConstraintSql = """
        DO $do$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ex_variant_preparations_one_default') THEN
                ALTER TABLE catalog.variant_preparations ADD CONSTRAINT ex_variant_preparations_one_default
                    EXCLUDE USING gist (variant_id WITH =) WHERE (is_default AND is_active) DEFERRABLE INITIALLY DEFERRED;
            END IF;
        END
        $do$;
        """;

    /// <summary>
    /// D3: <c>catalog.storefront_offers</c>, one row per price period of a visible variant (ACTIVE product, active
    /// category and parent, active variant with an active preparation) at a fulfillment location, with its stock item.
    /// Readers filter the period with the command's instant (<c>valid_from ≤ t &lt; valid_to</c>) so the app clock rules.
    /// While Inventory's <c>inventory.stock_items</c> does not exist the view is created without stock
    /// (<c>has_stock_item = false</c>, nothing is sold); re-running this SQL after the table exists adds it.
    /// </summary>
    public const string StorefrontOffersViewSql = """
        DO $do$
        DECLARE
            stock_join text;
            stock_columns text;
        BEGIN
            IF to_regclass('inventory.stock_items') IS NOT NULL THEN
                stock_columns := 'si.id IS NOT NULL AS has_stock_item, CAST(greatest(si.on_hand - si.reserved, 0) AS integer) AS available, CAST(si.low_stock_threshold AS integer) AS low_stock_threshold';
                stock_join := 'LEFT JOIN inventory.stock_items si ON si.variant_id = v.id AND si.fulfillment_location_id = vp.fulfillment_location_id';
            ELSE
                stock_columns := 'false AS has_stock_item, CAST(NULL AS integer) AS available, CAST(NULL AS integer) AS low_stock_threshold';
                stock_join := '';
            END IF;
            EXECUTE format($view$
                CREATE OR REPLACE VIEW catalog.storefront_offers AS
                SELECT vp.fulfillment_location_id, p.id AS product_id, v.id AS variant_id, v.sell_unit,
                       vp.unit_price_paise, vp.compare_at_paise, vp.valid_from, vp.valid_to, %s
                FROM catalog.products p
                JOIN catalog.categories c ON c.id = p.category_id AND c.is_active
                LEFT JOIN catalog.categories pc ON pc.id = c.parent_id
                JOIN catalog.product_variants v ON v.product_id = p.id AND v.is_active
                JOIN pricing.variant_prices vp ON vp.variant_id = v.id
                %s
                WHERE p.status = 'ACTIVE'
                  AND (c.parent_id IS NULL OR pc.is_active)
                  AND EXISTS (SELECT 1 FROM catalog.variant_preparations x WHERE x.variant_id = v.id AND x.is_active)
                $view$, stock_columns, stock_join);
        END
        $do$;
        """;

    /// <summary>Preparation charge periods for the storefront (PRC-R8: a missing row means the preparation cannot be ordered there).</summary>
    public const string StorefrontChargesViewSql = """
        CREATE OR REPLACE VIEW catalog.storefront_preparation_charges AS
        SELECT pc.fulfillment_location_id, pc.variant_id, pc.preparation_method_id, pc.charge_paise, pc.charge_basis, pc.valid_from, pc.valid_to
        FROM pricing.preparation_charges pc;
        """;

    /// <summary>docs/api §1.4 location context: served pincodes (active pincode, area, ACTIVE location in an ACTIVE city).</summary>
    public const string StorefrontLocationsViewSql = """
        CREATE OR REPLACE VIEW catalog.storefront_locations AS
        SELECT CAST(p.pincode AS text) AS pincode, a.id AS service_area_id, l.id AS fulfillment_location_id, l.code AS fulfillment_location_code
        FROM locations.pincodes p
        JOIN locations.service_areas a ON a.id = p.service_area_id AND a.is_active
        JOIN locations.fulfillment_locations l ON l.id = a.fulfillment_location_id AND l.status = 'ACTIVE'
        JOIN locations.cities c ON c.id = l.city_id AND c.status = 'ACTIVE'
        WHERE p.is_active;
        """;

    public string Schema => SchemaName;

    public IEnumerable<string> DatabaseObjectsSql =>
    [
        ArrayToStringFunctionSql,
        SearchSql,
        DefaultPreparationConstraintSql,
        StorefrontOffersViewSql,
        StorefrontChargesViewSql,
        StorefrontLocationsViewSql,
    ];

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogModuleModel).Assembly);
    }
}
