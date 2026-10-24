using FreshOrbit.Identity.Domain.Otp;
using FreshOrbit.Identity.Domain.Roles;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.Identity.Domain.Tokens;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.SharedKernel.Codes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Identity.Infrastructure.Persistence.Configurations;

internal static class Checks
{
    public static string In<TEnum>(string column)
        where TEnum : struct, Enum
        => $"{column} IN ({string.Join(", ", EnumCodes.AllCodes<TEnum>().Select(c => $"'{c}'"))})";
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t =>
        {
            t.HasCheckConstraint("ck_users_user_type", Checks.In<UserType>("user_type"));
            t.HasCheckConstraint("ck_users_status", Checks.In<UserStatus>("status"));
            t.HasCheckConstraint("ck_users_phone_or_email", "phone IS NOT NULL OR email IS NOT NULL OR anonymised_at IS NOT NULL");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Phone).HasMaxLength(16);
        builder.Property(x => x.Email).HasColumnType("citext").HasMaxLength(254);
        builder.Property(x => x.PasswordHash).HasMaxLength(200);
        builder.HasIndex(x => x.Phone).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();
    }
}

/// <summary>IDN-R20: append-only; read by (email hash, client IP, newest first) for the per email + IP limit.</summary>
internal sealed class StaffLoginFailureConfiguration : IEntityTypeConfiguration<StaffLoginFailure>
{
    public void Configure(EntityTypeBuilder<StaffLoginFailure> builder)
    {
        builder.ToTable("staff_login_failures");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EmailHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.ClientIp).HasMaxLength(45).IsRequired();
        builder.HasIndex(x => new { x.EmailHash, x.ClientIp, x.FailedAt }).IsDescending(false, false, true);
    }
}

internal sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("otp_challenges", t => t.HasCheckConstraint("ck_otp_challenges_purpose", Checks.In<OtpPurpose>("purpose")));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Phone).HasMaxLength(16).IsRequired();
        builder.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequestIp).HasMaxLength(45);
        builder.HasIndex(x => new { x.Phone, x.RequestedAt }).IsDescending(false, true);
        builder.HasIndex(x => new { x.RequestIp, x.RequestedAt }).IsDescending(false, true);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", t => t.HasCheckConstraint("ck_refresh_tokens_revoke_reason", $"revoke_reason IS NULL OR {Checks.In<RevokeReason>("revoke_reason")}"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DeviceLabel).HasMaxLength(100);
        builder.Ignore(x => x.WasRotated);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.FamilyId);
        builder.HasIndex(x => x.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> builder)
    {
        builder.ToTable("staff_members");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FullName).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(x => x.RoleIds);
        builder.Ignore(x => x.LocationIds);

        builder.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.StaffMemberId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Roles).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        builder.HasMany(x => x.Locations).WithOne().HasForeignKey(x => x.StaffMemberId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Locations).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class StaffRoleConfiguration : IEntityTypeConfiguration<StaffRole>
{
    public void Configure(EntityTypeBuilder<StaffRole> builder)
    {
        builder.ToTable("staff_roles");
        builder.HasKey(x => new { x.StaffMemberId, x.RoleId });
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.RoleId);
    }
}

internal sealed class StaffLocationAccessConfiguration : IEntityTypeConfiguration<StaffLocationAccess>
{
    public void Configure(EntityTypeBuilder<StaffLocationAccess> builder)
    {
        builder.ToTable("staff_location_access");
        builder.HasKey(x => new { x.StaffMemberId, x.FulfillmentLocationId });
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Ignore(x => x.IsAdmin);
        builder.Ignore(x => x.PermissionCodes);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasMany(x => x.Permissions).WithOne().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(x => new { x.RoleId, x.PermissionCode });
        builder.Property(x => x.PermissionCode).HasMaxLength(60);
        builder.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionCode).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("code").HasMaxLength(60).ValueGeneratedNever();
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
    }
}
