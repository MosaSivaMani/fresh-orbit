using FreshOrbit.SharedKernel.EntityFramework.Platform;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.SharedKernel.EntityFramework.Seeding;

/// <summary>Helpers for loaders: the table name of an entity type and the sample rows already recorded for it.</summary>
public static class SampleTracking
{
    /// <summary><c>schema.table</c> of <typeparamref name="TEntity"/> in the model.</summary>
    public static string TableOf<TEntity>(DbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        var type = db.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not part of the model.");
        return $"{type.GetSchema()}.{type.GetTableName()}";
    }

    /// <summary>Tracks each id as a sample row of <typeparamref name="TEntity"/>'s table.</summary>
    public static void Track<TEntity>(this SampleDataContext context, DbContext db, params IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ids);
        var table = TableOf<TEntity>(db);
        foreach (var id in ids)
        {
            context.Tracker.Track(table, id);
        }
    }

    /// <summary>Ids of <typeparamref name="TEntity"/>'s table already recorded in <c>platform.sample_records</c> (one query).</summary>
    public static async Task<HashSet<Guid>> TrackedIdsAsync<TEntity>(DbContext db, CancellationToken cancellationToken)
    {
        var table = TableOf<TEntity>(db);
        var ids = await db.Set<SampleRecord>().AsNoTracking().Where(r => r.TableName == table).Select(r => r.RecordId).ToListAsync(cancellationToken);
        return [.. ids];
    }

    /// <summary>Sample ids recorded for <c>schema.table</c> in a purge's record set (empty when none).</summary>
    public static IReadOnlyList<Guid> IdsOf(this IReadOnlyDictionary<string, Guid[]> records, string tableName)
    {
        ArgumentNullException.ThrowIfNull(records);
        return records.TryGetValue(tableName, out var ids) ? ids : [];
    }
}
