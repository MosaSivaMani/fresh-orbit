using System.Text.RegularExpressions;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshOrbit.SharedKernel.EntityFramework.Seeding;

/// <summary>
/// Deletes every row listed in <c>platform.sample_records</c> (by <c>id</c>), table by table. A table whose delete
/// hits a foreign key (another sample row still points at it) is retried after the others, until nothing is left
/// or no table can make progress.
/// </summary>
public sealed partial class SampleDataPurger(FreshOrbitDbContext db)
{
    private const string ForeignKeyViolation = "23503";

    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var records = await db.Set<SampleRecord>().AsNoTracking().ToListAsync(cancellationToken);
        var pending = records.GroupBy(r => r.TableName).ToDictionary(g => g.Key, g => g.Select(r => r.RecordId).ToArray());
        var deleted = 0;
        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var (table, ids) in pending.ToList())
            {
                EnsureTableName(table);
                try
                {
                    // The table name is validated by EnsureTableName (identifier pattern); ids are a parameter.
                    var sql = "DELETE FROM " + table + " WHERE id = ANY(@ids)";
                    deleted += await db.Database.ExecuteSqlRawAsync(
                        sql,
                        [new NpgsqlParameter("ids", ids)],
                        cancellationToken);
                    await db.Set<SampleRecord>().Where(r => r.TableName == table).ExecuteDeleteAsync(cancellationToken);
                    pending.Remove(table);
                    progressed = true;
                }
                catch (PostgresException exception) when (exception.SqlState == ForeignKeyViolation)
                {
                    // Another sample table still references these rows; try again after it is purged.
                }
            }

            if (!progressed)
            {
                throw new InvalidOperationException($"Sample rows in {string.Join(", ", pending.Keys)} are still referenced by non-sample rows.");
            }
        }

        return deleted;
    }

    internal static void EnsureTableName(string tableName)
    {
        if (!TableNamePattern().IsMatch(tableName))
        {
            throw new ArgumentException($"'{tableName}' is not a schema-qualified snake_case table name.", nameof(tableName));
        }
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}\\.[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex TableNamePattern();
}
