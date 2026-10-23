using System.Diagnostics;
using FreshOrbit.Reporting.Domain;
using FreshOrbit.Reporting.Infrastructure.Persistence;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshOrbit.Reporting.Infrastructure.Jobs;

internal sealed class ReportingJobRegistration : IRecurringJobRegistration
{
    public const string RefreshJobId = "reporting.refresh";

    public IEnumerable<RecurringJobDefinition> GetJobs() => [new RecurringJobDefinition(RefreshJobId, "*/15 * * * *", typeof(RefreshReportsJob))];
}

/// <summary>What one refresh run did.</summary>
public sealed record RefreshResult(bool Skipped, IReadOnlyList<string> Refreshed, IReadOnlyList<string> Failed);

/// <summary>
/// RefreshReports (REP-R9), every 15 minutes, single instance (Postgres advisory lock). Reads <c>refresh_state</c> and the
/// source watermark (one SQL); when nothing is newer it exits without refreshing. Otherwise each view is refreshed
/// <c>CONCURRENTLY</c> (readers keep the previous data), a failing view keeps its data and records <c>last_error</c>,
/// and <c>refresh_state</c> is written once.
/// </summary>
public sealed class RefreshReportsJob(FreshOrbitDbContext db, IClock clock) : IRecurringJob
{
    private const long LockKey = 0x5245504F52545346; // "REPORTSF"

    private const string WatermarkSql = """
        SELECT greatest(
            (SELECT max(coalesce(modified_at, created_at)) FROM ordering.orders),
            (SELECT max(created_at) FROM inventory.stock_movements),
            (SELECT max(coalesce(modified_at, created_at)) FROM payments.refunds)) AS "Value"
        """;

    private static readonly Dictionary<string, (string Concurrent, string Full)> RefreshSql = ReportViews.All.ToDictionary(
        v => v.Name,
        v => ($"REFRESH MATERIALIZED VIEW CONCURRENTLY {v.QualifiedName}", $"REFRESH MATERIALIZED VIEW {v.QualifiedName}"),
        StringComparer.Ordinal);

    public async Task ExecuteAsync(CancellationToken cancellationToken) => await RunAsync(cancellationToken);

    public async Task<RefreshResult> RunAsync(CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var acquired = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock({LockKey}) AS \"Value\"").SingleAsync(cancellationToken);
            if (!acquired)
            {
                return new RefreshResult(true, [], []);
            }

            try
            {
                return await RefreshAsync(cancellationToken);
            }
            finally
            {
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({LockKey})", CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<RefreshResult> RefreshAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset? watermark;
        try
        {
            watermark = await db.Database.SqlQueryRaw<DateTimeOffset?>(WatermarkSql).SingleAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return new RefreshResult(true, [], []);
        }

        var states = await db.Set<RefreshState>().ToDictionaryAsync(s => s.ViewName, StringComparer.Ordinal, cancellationToken);
        var populated = new Dictionary<string, bool>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand("SELECT matviewname, ispopulated FROM pg_matviews WHERE schemaname = @schema", (NpgsqlConnection)db.Database.GetDbConnection()))
        {
            command.Parameters.AddWithValue("schema", ReportingModuleModel.SchemaName);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                populated[reader.GetString(0)] = reader.GetBoolean(1);
            }
        }

        var refreshed = new List<string>();
        var failed = new List<string>();
        foreach (var view in ReportViews.All)
        {
            if (!states.TryGetValue(view.Name, out var state))
            {
                state = RefreshState.Never(view.Name);
                db.Add(state);
                states[view.Name] = state;
            }

            if (!state.NeedsRefresh(watermark))
            {
                continue;
            }

            if (!populated.TryGetValue(view.Name, out var isPopulated))
            {
                state.Failed("VIEW_MISSING: the materialized view does not exist (its source tables were missing when the SQL ran).");
                failed.Add(view.Name);
                continue;
            }

            var started = Stopwatch.GetTimestamp();
            try
            {
                var sql = RefreshSql[view.Name];
                await db.Database.ExecuteSqlRawAsync(isPopulated ? sql.Concurrent : sql.Full, cancellationToken);
                state.Refreshed(clock.UtcNow, watermark, (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                refreshed.Add(view.Name);
            }
            catch (PostgresException exception)
            {
                state.Failed($"{exception.SqlState}: {exception.MessageText}");
                failed.Add(view.Name);
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new RefreshResult(refreshed.Count == 0 && failed.Count == 0, refreshed, failed);
    }
}
