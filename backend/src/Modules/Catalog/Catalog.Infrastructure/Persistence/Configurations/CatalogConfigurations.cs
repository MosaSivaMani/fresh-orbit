using FreshOrbit.Catalog.Domain.Categories;
using FreshOrbit.Catalog.Domain.Preparations;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Catalog.Infrastructure.Persistence.Configurations;

internal static class CodeCheck
{
    /// <summary>CHECK constraint SQL limiting <paramref name="column"/> to the codes of <typeparamref name="TEnum"/>.</summary>
    public static string In<TEnum>(string column)
        where TEnum : struct, Enum
        => $"{column} IN ({string.Join(", ", EnumCodes.AllCodes<TEnum>().Select(c => $"'{c}'"))})";
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public const string SlugIndex = "ux_categories_slug";

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Slug).HasMaxLength(Slugs.MaxLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(Category.DescriptionMaxLength);
        builder.Property(x => x.ImageKey).HasMaxLength(512);
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName(SlugIndex);
        builder.HasIndex(x => x.ParentId);
        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PreparationMethodConfiguration : IEntityTypeConfiguration<PreparationMethod>
{
    public void Configure(EntityTypeBuilder<PreparationMethod> builder)
    {
        builder.ToTable("preparation_methods", t => t.HasCheckConstraint("ck_preparation_methods_guidance_yield", "guidance_yield_bp BETWEEN 1 AND 10000"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_preparation_methods_code");
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public const string SlugIndex = "ux_products_slug";

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", t =>
        {
            t.HasCheckConstraint("ck_products_status", CodeCheck.In<ProductStatus>("status"));
            t.HasCheckConstraint("ck_products_source_type", CodeCheck.In<SourceType>("source_type"));
            t.HasCheckConstraint("ck_products_hsn_code", "hsn_code IS NULL OR hsn_code ~ '^[0-9]{4,8}$'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Slug).HasMaxLength(Slugs.MaxLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(x => x.LocalNames).HasColumnType("text[]").IsRequired();
        builder.Property(x => x.SpeciesScientificName).HasMaxLength(Product.ShortTextMaxLength);
        builder.Property(x => x.Description).HasMaxLength(Product.TextMaxLength);
        builder.Property(x => x.SourceRegion).HasMaxLength(Product.ShortTextMaxLength);
        builder.Property(x => x.StorageInstructions).HasMaxLength(Product.TextMaxLength);
        builder.Property(x => x.HandlingNotes).HasMaxLength(Product.TextMaxLength);
        builder.Property(x => x.HsnCode).HasMaxLength(8);
        builder.Property(x => x.TaxCategoryCode).HasMaxLength(40);
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName(SlugIndex);
        builder.HasIndex(x => x.CategoryId);
        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Variants).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Images).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(x => x.PrimaryImage);
    }
}

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public const string SkuIndex = "ux_product_variants_sku";

    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants", t =>
        {
            t.HasCheckConstraint("ck_product_variants_sell_unit", CodeCheck.In<SellUnit>("sell_unit"));
            t.HasCheckConstraint("ck_product_variants_stock_unit", CodeCheck.In<StockUnit>("stock_unit"));
            t.HasCheckConstraint("ck_product_variants_quantity_rules", "order_step > 0 AND min_order >= order_step AND min_order % order_step = 0 AND max_order >= min_order AND max_order % order_step = 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Sku).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(VariantRules.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.Sku).IsUnique().HasDatabaseName(SkuIndex);
        builder.HasIndex(x => x.ProductId);
        builder.HasMany(x => x.Preparations).WithOne().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Preparations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(x => x.DefaultPreparation);
    }
}

internal sealed class VariantPreparationConfiguration : IEntityTypeConfiguration<VariantPreparation>
{
    public void Configure(EntityTypeBuilder<VariantPreparation> builder)
    {
        builder.ToTable("variant_preparations", t => t.HasCheckConstraint("ck_variant_preparations_yield", "yield_bp BETWEEN 1 AND 10000"));
        builder.HasKey(x => new { x.VariantId, x.PreparationMethodId });
        builder.HasOne<PreparationMethod>().WithMany().HasForeignKey(x => x.PreparationMethodId).OnDelete(DeleteBehavior.Restrict);

        // "One default per variant" is a deferred EXCLUDE constraint in CatalogModuleModel.DatabaseObjectsSql, so a
        // single save may move the default from one row to another.
    }
}

internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.AltText).HasMaxLength(ProductImage.AltTextMaxLength).IsRequired();
        builder.HasIndex(x => new { x.ProductId, x.SortOrder });
    }
}
