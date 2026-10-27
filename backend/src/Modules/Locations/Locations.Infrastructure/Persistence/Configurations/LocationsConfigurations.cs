using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.FulfillmentLocations;
using FreshOrbit.Locations.Domain.ServiceAreas;
using FreshOrbit.Locations.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Locations.Infrastructure.Persistence.Configurations;

internal static class CodeCheck
{
    /// <summary>CHECK constraint SQL limiting <paramref name="column"/> to the codes of <typeparamref name="TEnum"/>.</summary>
    public static string In<TEnum>(string column)
        where TEnum : struct, Enum
        => $"{column} IN ({string.Join(", ", EnumCodes.AllCodes<TEnum>().Select(c => $"'{c}'"))})";
}

internal sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities", t => t.HasCheckConstraint("ck_cities_status", CodeCheck.In<CityStatus>("status")));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(Rules.CodeMaxLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(Rules.NameMaxLength).IsRequired();
        builder.Property(x => x.StateCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.StateName).HasMaxLength(Rules.StateNameMaxLength);
        builder.Property(x => x.TimeZone).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_cities_code");
    }
}

internal sealed class FulfillmentLocationConfiguration : IEntityTypeConfiguration<FulfillmentLocation>
{
    public void Configure(EntityTypeBuilder<FulfillmentLocation> builder)
    {
        builder.ToTable("fulfillment_locations", t => t.HasCheckConstraint("ck_fulfillment_locations_status", CodeCheck.In<LocationStatus>("status")));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(Rules.CodeMaxLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(Rules.NameMaxLength).IsRequired();
        builder.Property(x => x.AddressLine).HasMaxLength(FulfillmentLocation.AddressMaxLength);
        builder.Property(x => x.Pincode).HasMaxLength(Pincode.Length);
        builder.Property(x => x.Phone).HasMaxLength(FulfillmentLocation.PhoneMaxLength);
        builder.Property(x => x.Latitude).HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasPrecision(9, 6);
        builder.Property(x => x.FssaiLicenseNo).HasMaxLength(FulfillmentLocation.FssaiMaxLength);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_fulfillment_locations_code");
        builder.HasIndex(x => x.CityId);
        builder.HasOne<City>().WithMany().HasForeignKey(x => x.CityId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.OperatingHours).WithOne().HasForeignKey(x => x.FulfillmentLocationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.OperatingHours).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.Closures).WithOne().HasForeignKey(x => x.FulfillmentLocationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Closures).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class OperatingHoursConfiguration : IEntityTypeConfiguration<OperatingHours>
{
    public void Configure(EntityTypeBuilder<OperatingHours> builder)
    {
        builder.ToTable("operating_hours", t => t.HasCheckConstraint("ck_operating_hours_window", "opens_at < closes_at"));
        builder.HasKey(x => new { x.FulfillmentLocationId, x.DayOfWeek });
    }
}

internal sealed class LocationClosureConfiguration : IEntityTypeConfiguration<LocationClosure>
{
    public void Configure(EntityTypeBuilder<LocationClosure> builder)
    {
        builder.ToTable("location_closures");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Reason).HasMaxLength(LocationClosure.ReasonMaxLength);
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.ClosedOn }).IsUnique().HasDatabaseName("ux_location_closures_location_date");
    }
}

internal sealed class ServiceAreaConfiguration : IEntityTypeConfiguration<ServiceArea>
{
    public void Configure(EntityTypeBuilder<ServiceArea> builder)
    {
        builder.ToTable("service_areas", t =>
        {
            t.HasCheckConstraint("ck_service_areas_fees", "delivery_fee_paise >= 0 AND min_order_paise >= 0 AND (free_delivery_above_paise IS NULL OR free_delivery_above_paise >= min_order_paise)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(Rules.NameMaxLength).IsRequired();
        builder.HasIndex(x => x.FulfillmentLocationId);
        builder.HasIndex(x => x.CityId);
        builder.HasOne<City>().WithMany().HasForeignKey(x => x.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FulfillmentLocation>().WithMany().HasForeignKey(x => x.FulfillmentLocationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Pincodes).WithOne().HasForeignKey(x => x.ServiceAreaId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Pincodes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PincodeConfiguration : IEntityTypeConfiguration<Pincode>
{
    public const string ActivePincodeIndex = "ux_pincodes_active_pincode";

    public void Configure(EntityTypeBuilder<Pincode> builder)
    {
        builder.ToTable("pincodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasColumnName("pincode").HasMaxLength(Pincode.Length).IsFixedLength().IsRequired();

        // LOC-R3: a pincode is active in at most one service area; the command checks first, this index is the last line.
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("is_active").HasDatabaseName(ActivePincodeIndex);
        builder.HasIndex(x => x.ServiceAreaId);
    }
}
