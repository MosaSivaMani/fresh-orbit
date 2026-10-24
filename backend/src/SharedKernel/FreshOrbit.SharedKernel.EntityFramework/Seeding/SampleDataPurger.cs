using System.Text.RegularExpressions;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshOrbit.SharedKernel.EntityFramework.Seeding;

/// <summary>
/// Deletes every row listed in <c>platform.sample_records</c> (by <c>id</c>), table by table, in one transaction.
/// First every <see cref="ISamplePurgeParticipant"/> is asked for blockers (real orders, carts, payments on sample
/// rows) and the purge is refused when any exist. A table whose delete hits a foreign key (another row still points at
/// it) is retried after the others; when no table can make progress the whole purge is rolled back and refused, naming
/// the tables and the constraint that blocked them. Nothing is half-deleted.
/// </summary>
public sealed partial class SampleDataPurger(FreshOrbitDbContext db, IEnumerable<ISamplePurgeParticipant> participants)
{
    private const string ForeignKeyViolation = "23503";

    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Set<SampleRecord>().AsNoTracking().ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        var records = rows.GroupBy(r => r.TableName).ToDictionary(g => g.Key, g => g.Select(r => r.RecordId).ToArray(), StringComparer.Ordinal);
        foreach (var table in records.Keys)
        {
            EnsureTableName(table);
        }

        var blockers = new List<string>();
        foreach (var participant in participants)
        {
            blockers.AddRange(await participant.FindBlockersAsync(records, cancellationToken));
        }

        if (blockers.Count > 0)
        {
            throw new SamplePurgeRefusedException(blockers);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var participant in participants)
        {
            await participant.PrepareAsync(records, cancellationToken);
        }

        var pending = new Dictionary<string, Guid[]>(records, StringComparer.Ordinal);
        var failures = new Dictionary<string, string>(StringComparer.Ordinal);
        var deleted = 0;
        var savepoint = 0;
        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var (table, ids) in pending.ToList())
            {
                var name = $"purge_{++savepoint}";
                await transaction.CreateSavepointAsync(name, cancellationToken);
                try
                {
                    // The table name is validated by EnsureTableName (identifier pattern); ids are a parameter.
                    var sql = "DELETE FROM " + table + " WHERE id = ANY(@ids)";
                    deleted += await db.Database.ExecuteSqlRawAsync(sql, [new NpgsqlParameter("ids", ids)], cancellationToken);
                    await db.Set<SampleRecord>().Where(r => r.TableName == table).ExecuteDeleteAsync(cancellationToken);
                    await transaction.ReleaseSavepointAsync(name, cancellationToken);
                    pending.Remove(table);
                    failures.Remove(table);
                    progressed = true;
                }
                catch (PostgresException exception) when (exception.SqlState == ForeignKeyViolation)
                {
                    // Another table still references these rows; try again after it is purged.
                    await transaction.RollbackToSavepointAsync(name, cancellationToken);
                    failures[table] = exception.MessageText;
                }
            }

            if (!progressed)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new SamplePurgeRefusedException([.. pending.Keys.Order(StringComparer.Ordinal).Select(t => $"{t} is still referenced ({failures.GetValueOrDefault(t, "foreign key")}).")]);
            }
        }

        await transaction.CommitAsync(cancellationToken);
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
