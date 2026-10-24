using FreshOrbit.Customers.Domain.Addresses;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Customers.Domain.StockAlerts;
using FreshOrbit.Customers.Domain.Wishlist;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Customers.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FullName).HasMaxLength(100);
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}

internal sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses", t =>
        {
            t.HasCheckConstraint("ck_addresses_coordinates", "latitude IS NULL OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)");
            t.HasCheckConstraint("ck_addresses_default_active", "NOT is_default OR is_active");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RecipientName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Line1).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Line2).HasMaxLength(200);
        builder.Property(x => x.Landmark).HasMaxLength(200);
        builder.Property(x => x.Locality).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CityName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StateCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.Pincode).HasMaxLength(6).IsRequired();
        builder.Property(x => x.Latitude).HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasPrecision(9, 6);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.IsActive });

        // CUS-R4: at most one active default per customer.
        builder.HasIndex(x => x.CustomerId).IsUnique().HasFilter("is_default AND is_active").HasDatabaseName("ux_addresses_one_default");
    }
}

internal sealed class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("wishlist_items");
        builder.HasKey(x => new { x.CustomerId, x.ProductId });
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StockAlertConfiguration : IEntityTypeConfiguration<StockAlert>
{
    public void Configure(EntityTypeBuilder<StockAlert> builder)
    {
        builder.ToTable("stock_alerts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);

        // CUS-R9: one alert per customer, variant and location; the second index serves the back-in-stock handler.
        builder.HasIndex(x => new { x.CustomerId, x.VariantId, x.FulfillmentLocationId }).IsUnique();
        builder.HasIndex(x => new { x.VariantId, x.FulfillmentLocationId });
    }
}
