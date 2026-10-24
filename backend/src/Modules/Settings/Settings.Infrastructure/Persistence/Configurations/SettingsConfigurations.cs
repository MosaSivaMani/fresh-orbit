using FreshOrbit.Settings.Domain.BusinessProfiles;
using FreshOrbit.Settings.Domain.Numbering;
using FreshOrbit.Settings.Domain.OrderPolicies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Settings.Infrastructure.Persistence.Configurations;

internal sealed class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>
{
    public void Configure(EntityTypeBuilder<BusinessProfile> builder)
    {
        builder.ToTable("business_profile");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.LegalName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TradeName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Gstin).HasMaxLength(15);
        builder.Property(x => x.FssaiLicenseNo).HasMaxLength(14);
        builder.Property(x => x.Address).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SupportPhone).HasMaxLength(16).IsRequired();
        builder.Property(x => x.SupportEmail).HasMaxLength(254).IsRequired();
        builder.Property(x => x.SupportHours).HasMaxLength(200);
        builder.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();
    }
}

internal sealed class OrderPolicyConfiguration : IEntityTypeConfiguration<OrderPolicy>
{
    public void Configure(EntityTypeBuilder<OrderPolicy> builder)
    {
        builder.ToTable("order_policies", t =>
        {
            t.HasCheckConstraint("ck_order_policies_cancel_status", "cancel_allowed_until_status IN ('PENDING_PAYMENT', 'CONFIRMED', 'PREPARING', 'PACKED')");
            t.HasCheckConstraint("ck_order_policies_ranges", "reservation_minutes > 0 AND max_overweight_bp >= 0 AND cod_max_order_paise >= 0 AND substitution_response_minutes > 0 AND complaint_window_hours > 0 AND max_delivery_attempts > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CancelAllowedUntilStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.EnabledPaymentMethods).HasColumnType("text[]").IsRequired();
        builder.HasIndex(x => x.CityId).IsUnique().AreNullsDistinct(false);
    }
}

internal sealed class NumberSeriesConfiguration : IEntityTypeConfiguration<NumberSeries>
{
    public void Configure(EntityTypeBuilder<NumberSeries> builder)
    {
        builder.ToTable("number_series", t =>
        {
            t.HasCheckConstraint("ck_number_series_code", "code IN ('ORDER', 'INVOICE')");
            t.HasCheckConstraint("ck_number_series_values", "next_value >= 1 AND pad_width BETWEEN 1 AND 10");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Period).HasMaxLength(7).IsRequired();
        builder.Property(x => x.Prefix).HasMaxLength(20).IsRequired();
        builder.Ignore(x => x.IsInUse);
        builder.HasIndex(x => new { x.Code, x.Period }).IsUnique();
    }
}
