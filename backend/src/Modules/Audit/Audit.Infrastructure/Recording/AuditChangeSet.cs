using System.Text;
using System.Text.Json;
using FreshOrbit.Audit.Domain.Entries;
using FreshOrbit.Audit.Infrastructure.Persistence;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FreshOrbit.Audit.Infrastructure.Recording;

/// <summary>One changed entity as it will be written to <c>audit.entries</c>.</summary>
public sealed record AuditChange(string Action, string EntityType, string EntityId, string? Before, string? After);

/// <summary>
/// Reads the pending changes of a context (in memory, no database call) and turns each added, modified or deleted
/// entity into an <see cref="AuditChange"/>: before/after hold only the changed properties for an update, every
/// property for a create or delete, with sensitive values redacted (<see cref="AuditRedaction"/>).
/// </summary>
public static class AuditChangeSet
{
    private static readonly HashSet<string> SkippedSchemas = new(StringComparer.Ordinal)
    {
        AuditModuleModel.SchemaName,
        PlatformModuleModel.SchemaName,
    };

    private static readonly HashSet<string> BookkeepingProperties = new(StringComparer.Ordinal)
    {
        AuditColumns.CreatedAt, AuditColumns.CreatedBy, AuditColumns.ModifiedAt, AuditColumns.ModifiedBy, AuditColumns.Version,
    };

    public static List<AuditChange> Read(DbContext context, string? action)
    {
        var candidates = new List<(EntityEntry Entry, string EntityType, string EntityId)>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)
                || entry.Metadata.GetSchema() is { } schema && SkippedSchemas.Contains(schema))
            {
                continue;
            }

            candidates.Add((entry, EntityType(entry.Metadata), EntityId(entry)));
        }

        var covered = CoveredByExplicitRecords(context, candidates);
        var changes = new List<AuditChange>();
        foreach (var (entry, entityType, entityId) in candidates)
        {
            if (covered.Contains(entry))
            {
                continue;
            }

            var change = entry.State switch
            {
                EntityState.Added => Create(entry, action, "CREATED", entityId, before: null, after: Snapshot(entry, entry.Properties, current: true)),
                EntityState.Deleted => Create(entry, action, "DELETED", entityId, before: Snapshot(entry, entry.Properties, current: false), after: null),
                _ => Modified(entry, action, entityId),
            };
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        return changes;
    }

    /// <summary>
    /// AUD-R9: the changed entities that an explicit <c>IAuditApi.Record</c> in this save already describes. An explicit
    /// record (type T, id I) matches the entities whose UPPER_SNAKE class name C equals T or is its suffix
    /// (<c>NOTIFICATION_TEMPLATE</c> matches class <c>Template</c>). Among those, the one whose primary key is I is
    /// covered. When none has that key (the record uses a natural key or a singleton id, e.g. <c>default</c>,
    /// <c>code/period</c>), every changed entity of that class in the save is covered.
    /// </summary>
    private static HashSet<EntityEntry> CoveredByExplicitRecords(DbContext context, IReadOnlyList<(EntityEntry Entry, string EntityType, string EntityId)> candidates)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidates);
        var covered = new HashSet<EntityEntry>(ReferenceEqualityComparer.Instance);
        foreach (var record in context.ChangeTracker.Entries<AuditEntry>().Where(e => e.State == EntityState.Added).Select(e => e.Entity))
        {
            var sameType = candidates.Where(c => TypeMatches(record.EntityType, c.EntityType)).ToList();
            var byKey = sameType.Where(c => string.Equals(c.EntityId, record.EntityId, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var match in byKey.Count > 0 ? byKey : sameType)
            {
                covered.Add(match.Entry);
            }
        }

        return covered;
    }

    private static bool TypeMatches(string recordType, string entityType)
    {
        var normalized = recordType.ToUpperInvariant();
        return normalized == entityType || normalized.EndsWith("_" + entityType, StringComparison.Ordinal);
    }

    /// <summary>UPPER_SNAKE of the CLR type name, e.g. <c>StaffMember</c> → <c>STAFF_MEMBER</c> (AUD-R1).</summary>
    public static string EntityType(IReadOnlyTypeBase type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var name = type.ClrType.Name;
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsAsciiLetterUpper(c) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        var result = builder.ToString();
        return result.Length <= AuditEntry.MaxEntityTypeLength ? result : result[..AuditEntry.MaxEntityTypeLength].TrimEnd('_');
    }

    private static AuditChange? Modified(EntityEntry entry, string? action, string entityId)
    {
        var changed = entry.Properties.Where(p => p.IsModified && !BookkeepingProperties.Contains(p.Metadata.Name)).ToList();
        if (changed.Count == 0)
        {
            return null;
        }

        return Create(entry, action, "UPDATED", entityId, Snapshot(entry, changed, current: false), Snapshot(entry, changed, current: true));
    }

    private static AuditChange Create(EntityEntry entry, string? action, string verb, string entityId, string? before, string? after)
    {
        var entityType = EntityType(entry.Metadata);
        return new AuditChange(action ?? $"{entityType}_{verb}", entityType, entityId, before, after);
    }

    private static string EntityId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return "unknown";
        }

        var parts = key.Properties.Select(p =>
        {
            var property = entry.Property(p.Name);
            return property.IsTemporary ? "new" : Convert.ToString(property.CurrentValue, System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        });
        var id = string.Join(':', parts);
        return id.Length <= AuditEntry.MaxEntityIdLength ? id : id[..AuditEntry.MaxEntityIdLength];
    }

    private static string Snapshot(EntityEntry entry, IEnumerable<PropertyEntry> properties, bool current)
    {
        var values = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            var name = property.Metadata.Name;
            if (BookkeepingProperties.Contains(name))
            {
                continue;
            }

            var key = JsonNamingPolicy.CamelCase.ConvertName(name);
            if (AuditRedaction.IsSensitive(name))
            {
                values[key] = AuditRedaction.Placeholder;
                continue;
            }

            var value = current || entry.State == EntityState.Added ? property.CurrentValue : property.OriginalValue;
            values[key] = ToJsonValue(value);
        }

        return JsonSerializer.Serialize(values, FreshOrbitJson.Options);
    }

    private static object? ToJsonValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is byte[] bytes)
        {
            return $"[{bytes.Length} bytes]";
        }

        try
        {
            return JsonSerializer.SerializeToElement(value, value.GetType(), FreshOrbitJson.Options);
        }
        catch (Exception exception) when (exception is NotSupportedException or JsonException or InvalidOperationException)
        {
            return value.ToString();
        }
    }
}
