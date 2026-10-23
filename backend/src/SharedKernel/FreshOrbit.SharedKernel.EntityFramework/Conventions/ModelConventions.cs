using FreshOrbit.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FreshOrbit.SharedKernel.EntityFramework.Conventions;

/// <summary>
/// Conventions applied to every entity type after all module models are configured:
/// audit shadow columns, <c>xmin</c> row version on aggregate roots, enums as UPPER_SNAKE codes (varchar 40)
/// and <see cref="DateTimeOffset"/> as UTC <c>timestamptz</c>.
/// </summary>
internal static class ModelConventions
{
    public const int CodeMaxLength = 40;

    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (entityType.IsOwned() || entityType.BaseType is not null || entityType.FindPrimaryKey() is null)
            {
                ApplyValueConventions(entityType);
                continue;
            }

            AddAuditColumns(entityType);
            if (typeof(IAggregateRoot).IsAssignableFrom(entityType.ClrType))
            {
                AddRowVersion(entityType);
            }

            ApplyValueConventions(entityType);
        }
    }

    private static void AddAuditColumns(IMutableEntityType entityType)
    {
        var createdAt = entityType.FindProperty(AuditColumns.CreatedAt) ?? entityType.AddProperty(AuditColumns.CreatedAt, typeof(DateTimeOffset));
        createdAt.IsNullable = false;
        if (entityType.FindProperty(AuditColumns.CreatedBy) is null)
        {
            entityType.AddProperty(AuditColumns.CreatedBy, typeof(Guid?));
        }

        if (entityType.FindProperty(AuditColumns.ModifiedAt) is null)
        {
            entityType.AddProperty(AuditColumns.ModifiedAt, typeof(DateTimeOffset?));
        }

        if (entityType.FindProperty(AuditColumns.ModifiedBy) is null)
        {
            entityType.AddProperty(AuditColumns.ModifiedBy, typeof(Guid?));
        }
    }

    private static void AddRowVersion(IMutableEntityType entityType)
    {
        var property = entityType.FindProperty(AuditColumns.Version)
            ?? entityType.AddProperty(entityType.ClrType.GetProperty(nameof(IAggregateRoot.Version))!);
        property.IsConcurrencyToken = true;
        property.ValueGenerated = ValueGenerated.OnAddOrUpdate;
        property.SetColumnName("xmin");
        property.SetColumnType("xid");
    }

    private static void ApplyValueConventions(IMutableEntityType entityType)
    {
        foreach (var property in entityType.GetDeclaredProperties())
        {
            if (property.GetValueConverter() is not null || property.GetProviderClrType() is not null)
            {
                continue;
            }

            var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (clrType.IsEnum)
            {
                var converter = (ValueConverter)Activator.CreateInstance(typeof(EnumCodeConverter<>).MakeGenericType(clrType))!;
                property.SetValueConverter(converter);
                property.SetMaxLength(property.GetMaxLength() ?? CodeMaxLength);
            }
            else if (clrType == typeof(DateTimeOffset))
            {
                property.SetValueConverter(new UtcDateTimeOffsetConverter());
                property.SetColumnType("timestamp with time zone");
            }
        }
    }
}
