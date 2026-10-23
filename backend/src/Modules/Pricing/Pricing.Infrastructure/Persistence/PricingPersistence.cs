using FreshOrbit.Pricing.Domain.Periods;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Pricing.Infrastructure.Persistence;

internal static class PeriodColumns
{
    public const string ValidRangeCheck = "valid_to IS NULL OR valid_to > valid_from";
}

internal sealed class VariantPriceConfiguration : IEntityTypeConfiguration<VariantPrice>
{
    public void Configure(EntityTypeBuilder<VariantPrice> builder)
    {
        builder.ToTable("variant_prices", t =>
        {
            t.HasCheckConstraint("ck_variant_prices_valid_range", PeriodColumns.ValidRangeCheck);
            t.HasCheckConstraint("ck_variant_prices_amounts", "unit_price_paise BETWEEN 1 AND 5000000 AND (compare_at_paise IS NULL OR compare_at_paise > unit_price_paise)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.VariantId, x.ValidFrom });
        builder.HasIndex(x => x.VariantId);
    }
}

internal sealed class PreparationChargeConfiguration : IEntityTypeConfiguration<PreparationCharge>
{
    public void Configure(EntityTypeBuilder<PreparationCharge> builder)
    {
        builder.ToTable("preparation_charges", t =>
        {
            t.HasCheckConstraint("ck_preparation_charges_valid_range", PeriodColumns.ValidRangeCheck);
            t.HasCheckConstraint("ck_preparation_charges_charge", "charge_paise BETWEEN 0 AND 5000000");
            t.HasCheckConstraint("ck_preparation_charges_basis", $"charge_basis IN ({string.Join(", ", EnumCodes.AllCodes<ChargeBasis>().Select(c => $"'{c}'"))})");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PreparationCode).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.VariantId, x.PreparationMethodId, x.ValidFrom });
    }
}

internal sealed class TaxRuleConfiguration : IEntityTypeConfiguration<TaxRule>
{
    public void Configure(EntityTypeBuilder<TaxRule> builder)
    {
        builder.ToTable("tax_rules", t =>
        {
            t.HasCheckConstraint("ck_tax_rules_valid_range", PeriodColumns.ValidRangeCheck);
            t.HasCheckConstraint("ck_tax_rules_rate", "rate_bp BETWEEN 0 AND 2800");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TaxCategoryCode).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.TaxCategoryCode, x.ValidFrom });
    }
}

internal sealed class TaxCategoryConfiguration : IEntityTypeConfiguration<TaxCategory>
{
    public void Configure(EntityTypeBuilder<TaxCategory> builder)
    {
        builder.ToTable("tax_categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.AppliesTo).HasMaxLength(200);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_tax_categories_code");
    }
}

public sealed class PricingRepository(FreshOrbitDbContext db) : IPricingRepository
{
    public Task<List<VariantPrice>> GetPricesAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken)
    {
        var locations = fulfillmentLocationIds.Distinct().ToList();
        var variants = variantIds.Distinct().ToList();
        return db.Set<VariantPrice>().Where(p => locations.Contains(p.FulfillmentLocationId) && variants.Contains(p.VariantId)).ToListAsync(cancellationToken);
    }

    public Task<List<PreparationCharge>> GetChargesAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken)
    {
        var locations = fulfillmentLocationIds.Distinct().ToList();
        var variants = variantIds.Distinct().ToList();
        return db.Set<PreparationCharge>().Where(p => locations.Contains(p.FulfillmentLocationId) && variants.Contains(p.VariantId)).ToListAsync(cancellationToken);
    }

    public Task<List<TaxRule>> GetTaxRulesAsync(IReadOnlyCollection<string> taxCategoryCodes, CancellationToken cancellationToken)
    {
        var codes = taxCategoryCodes.Distinct().ToList();
        return db.Set<TaxRule>().Where(r => codes.Contains(r.TaxCategoryCode)).ToListAsync(cancellationToken);
    }

    public Task<bool> TaxCategoryExistsAsync(string code, CancellationToken cancellationToken)
        => db.Set<TaxCategory>().AnyAsync(c => c.Code == code, cancellationToken);

    public Task<List<TaxCategory>> ListTaxCategoriesAsync(CancellationToken cancellationToken)
        => db.Set<TaxCategory>().AsNoTracking().OrderBy(c => c.Code).ToListAsync(cancellationToken);

    public Task<List<Guid>> GetPricedVariantIdsAsync(Guid fulfillmentLocationId, DateTimeOffset at, CancellationToken cancellationToken)
        => db.Set<VariantPrice>()
            .Where(p => p.FulfillmentLocationId == fulfillmentLocationId && (p.ValidTo == null || p.ValidTo > at))
            .Select(p => p.VariantId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public void Add(PricePeriod period) => db.Add((object)period);

    public void Remove(PricePeriod period) => db.Remove((object)period);

    public void MarkChanged(PricePeriod period)
    {
        var entry = db.Entry(period);
        if (entry.State == EntityState.Unchanged)
        {
            entry.Property(AuditColumns.ModifiedAt).IsModified = true;
        }
    }
}
