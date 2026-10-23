using FreshOrbit.Promotions.Domain.Banners;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.Promotions.Domain.Redemptions;
using FreshOrbit.SharedKernel.Codes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Promotions.Infrastructure.Persistence;

internal static class Checks
{
    /// <summary>CHECK expression limiting a code column to the enum's UPPER_SNAKE codes.</summary>
    public static string In<TEnum>(string column)
        where TEnum : struct, Enum
        => $"{column} IN ({string.Join(", ", EnumCodes.AllCodes<TEnum>().Select(c => $"'{c}'"))})";
}

internal sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupons", t =>
        {
            t.HasCheckConstraint("ck_coupons_used_count_within_total_limit", "total_limit IS NULL OR used_count <= total_limit");
            t.HasCheckConstraint("ck_coupons_used_count_not_negative", "used_count >= 0");
            t.HasCheckConstraint("ck_coupons_discount_type", Checks.In<DiscountType>("discount_type"));
            t.HasCheckConstraint("ck_coupons_status", Checks.In<CouponStatus>("status"));
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasColumnType("citext").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Description).HasMaxLength(Coupon.MaxDescriptionLength);
        builder.HasIndex(x => x.ValidTo).HasFilter("status IN ('ACTIVE', 'PAUSED')");
        builder.OwnsMany(x => x.Scopes, scope =>
        {
            scope.ToTable("coupon_scopes", t => t.HasCheckConstraint("ck_coupon_scopes_scope_type", Checks.In<CouponScopeType>("scope_type")));
            scope.WithOwner().HasForeignKey("CouponId");
            scope.Property<Guid>("CouponId");
            scope.HasKey("CouponId", nameof(CouponScope.ScopeType), nameof(CouponScope.ScopeId));
        });
        builder.Navigation(x => x.Scopes).HasField("_scopes").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CouponRedemptionConfiguration : IEntityTypeConfiguration<CouponRedemption>
{
    public void Configure(EntityTypeBuilder<CouponRedemption> builder)
    {
        builder.ToTable("coupon_redemptions", t =>
        {
            t.HasCheckConstraint("ck_coupon_redemptions_status", Checks.In<RedemptionStatus>("status"));
            t.HasCheckConstraint("ck_coupon_redemptions_reversal_reason", $"reversal_reason IS NULL OR {Checks.In<ReversalReason>("reversal_reason")}");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => new { x.CouponId, x.CustomerId });
        builder.HasOne<Coupon>().WithMany().HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("promotions", t => t.HasCheckConstraint("ck_promotions_link_type", Checks.In<PromotionLinkType>("link_type")));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(Promotion.MaxTitleLength).IsRequired();
        builder.Property(x => x.Subtitle).HasMaxLength(Promotion.MaxSubtitleLength);
        builder.Property(x => x.ImageKey).HasMaxLength(300);
        builder.Property(x => x.LinkTarget).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.CityId, x.StartsAt });
    }
}
