using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Pricing.Infrastructure.Persistence;

/// <summary>The Pricing module's tables, all in the <c>pricing</c> schema, plus its no-overlap constraints.</summary>
public sealed class PricingModuleModel : IModuleModel
{
    public const string SchemaName = "pricing";
    public const string PriceOverlapConstraint = "ex_variant_prices_no_overlap";
    public const string ChargeOverlapConstraint = "ex_preparation_charges_no_overlap";
    public const string TaxRuleOverlapConstraint = "ex_tax_rules_no_overlap";

    /// <summary>
    /// PRC-R2: no overlapping half-open periods per key (EXCLUDE gist with btree_gist), the last line behind the
    /// commands' own checks. Deferred to commit so one save may close a period and open the next in any order.
    /// </summary>
    public const string NoOverlapConstraintsSql = $"""
        DO $do$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = '{PriceOverlapConstraint}') THEN
                ALTER TABLE pricing.variant_prices ADD CONSTRAINT {PriceOverlapConstraint}
                    EXCLUDE USING gist (fulfillment_location_id WITH =, variant_id WITH =, tstzrange(valid_from, valid_to) WITH &&)
                    DEFERRABLE INITIALLY DEFERRED;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = '{ChargeOverlapConstraint}') THEN
                ALTER TABLE pricing.preparation_charges ADD CONSTRAINT {ChargeOverlapConstraint}
                    EXCLUDE USING gist (fulfillment_location_id WITH =, variant_id WITH =, preparation_method_id WITH =, tstzrange(valid_from, valid_to) WITH &&)
                    DEFERRABLE INITIALLY DEFERRED;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = '{TaxRuleOverlapConstraint}') THEN
                ALTER TABLE pricing.tax_rules ADD CONSTRAINT {TaxRuleOverlapConstraint}
                    EXCLUDE USING gist (tax_category_code WITH =, tstzrange(valid_from, valid_to) WITH &&)
                    DEFERRABLE INITIALLY DEFERRED;
            END IF;
        END
        $do$;
        """;

    public string Schema => SchemaName;

    public IEnumerable<string> DatabaseObjectsSql => [NoOverlapConstraintsSql];

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PricingModuleModel).Assembly);
    }
}
