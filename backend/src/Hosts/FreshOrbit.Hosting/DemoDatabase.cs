using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FreshOrbit.Hosting;

/// <summary>What <see cref="DemoDatabase.InspectAsync"/> found.</summary>
public enum DemoDatabaseState
{
    /// <summary>No table in any schema: a brand-new database the demo may claim.</summary>
    Empty,

    /// <summary>Carries the demo marker: created empty by the demo, holds only sample and tester data.</summary>
    Demo,

    /// <summary>Has tables and no demo marker: possibly real data; the demo never touches it.</summary>
    NotDemo,
}

/// <summary>
/// The Demo environment shows every SMS (OTPs included) at <c>/api/v1/dev/sms</c>, so it must never run on a database
/// that can hold real customers. The demo claims only a database that is completely empty: the Migrations host marks
/// it (a comment on the database, <see cref="Marker"/>) before the first migration, and the Demo Api and Migrations
/// host refuse any database without the mark. A production or staging database always has tables and never has the
/// mark, so pointing the demo at it by mistake stops at start-up with nothing changed.
/// </summary>
public static class DemoDatabase
{
    /// <summary>The comment on the database (<c>COMMENT ON DATABASE</c>) that marks it as the demo's.</summary>
    public const string Marker = "FRESH_ORBIT_DEMO";

    private const string ReadMarkerSql = "SELECT pg_catalog.shobj_description(d.oid, 'pg_database') FROM pg_catalog.pg_database d WHERE d.datname = pg_catalog.current_database()";

    private const string CountTablesSql = "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog', 'information_schema')";

    private const string MarkSql = "DO $$ BEGIN EXECUTE format('COMMENT ON DATABASE %I IS %L', current_database(), '" + Marker + "'); END $$";

    public static async Task<DemoDatabaseState> InspectAsync(string connectionString, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return await InspectOpenAsync(connection, cancellationToken);
    }

    /// <summary>Marks an empty database as the demo's; returns <see cref="DemoDatabaseState.Demo"/> or <see cref="DemoDatabaseState.NotDemo"/>.</summary>
    public static async Task<DemoDatabaseState> ClaimAsync(string connectionString, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var state = await InspectOpenAsync(connection, cancellationToken);
        if (state != DemoDatabaseState.Empty)
        {
            return state;
        }

        await using (var mark = new NpgsqlCommand(MarkSql, connection))
        {
            await mark.ExecuteNonQueryAsync(cancellationToken);
        }

        return await InspectOpenAsync(connection, cancellationToken);
    }

    private static async Task<DemoDatabaseState> InspectOpenAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using (var marker = new NpgsqlCommand(ReadMarkerSql, connection))
        {
            if (await marker.ExecuteScalarAsync(cancellationToken) is string comment && string.Equals(comment, Marker, StringComparison.Ordinal))
            {
                return DemoDatabaseState.Demo;
            }
        }

        await using var tables = new NpgsqlCommand(CountTablesSql, connection);
        var count = Convert.ToInt64(await tables.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        return count == 0 ? DemoDatabaseState.Empty : DemoDatabaseState.NotDemo;
    }
}

/// <summary>Demo Api: refuses to start (exit 1) unless the database carries the demo marker.</summary>
internal sealed partial class DemoDatabaseGuard(IConfiguration configuration, ILogger<DemoDatabaseGuard> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString(FreshOrbitDbContextOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new StartupRefusedException($"ConnectionStrings:{FreshOrbitDbContextOptions.ConnectionStringName} is required in the {DemoSetup.EnvironmentName} environment.");
        }

        var state = await DemoDatabase.InspectAsync(connectionString, cancellationToken);
        if (state != DemoDatabaseState.Demo)
        {
            throw new StartupRefusedException(
                $"The {DemoSetup.EnvironmentName} environment runs only on a database the demo created from empty (state {state}); give it a new, empty database and start the Migrations host first.");
        }

        LogDemoDatabase();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo environment: sample data only, every SMS is readable at /api/v1/dev/sms; never use this database for real customers")]
    private partial void LogDemoDatabase();
}
