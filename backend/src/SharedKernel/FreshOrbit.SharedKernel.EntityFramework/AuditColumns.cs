namespace FreshOrbit.SharedKernel.EntityFramework;

/// <summary>Shadow properties present on every entity type and stamped by <see cref="FreshOrbitDbContext"/>.</summary>
public static class AuditColumns
{
    public const string CreatedAt = nameof(CreatedAt);
    public const string CreatedBy = nameof(CreatedBy);
    public const string ModifiedAt = nameof(ModifiedAt);
    public const string ModifiedBy = nameof(ModifiedBy);

    /// <summary>The aggregate root row version property, mapped to Postgres <c>xmin</c>.</summary>
    public const string Version = "Version";
}
