using System.Collections.Concurrent;
using AwesomeAssertions;
using FreshOrbit.Hosting;
using FreshOrbit.Migrations;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FreshOrbit.SampleData.Tests;

/// <summary>
/// E2E-5: the Migrations host on a brand-new, empty database (not the fixture's EnsureCreated schema) logs no Error,
/// applies every migration, and a second run is clean too.
/// </summary>
public sealed class MigrationRunnerTests(SampleDatabase database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Empty_database_migrates_without_error_logs()
    {
        var connectionString = await CreateEmptyDatabaseAsync();
        var logs = new CapturingLoggerProvider();

        var first = await RunAsync(connectionString, logs);
        var second = await RunAsync(connectionString, logs);

        first.Should().Be(0);
        second.Should().Be(0);
        logs.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
        logs.Entries.Should().Contain(e => e.Category.StartsWith("Microsoft.EntityFrameworkCore.Migrations", StringComparison.Ordinal) && e.Message.Contains("InitialCreate", StringComparison.Ordinal));

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM platform.__ef_migrations_history", connection);
        ((long)(await command.ExecuteScalarAsync(Ct))!).Should().BeGreaterThan(0);
    }

    private async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = $"fo_migrate_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync(Ct);
        }

        return new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = name }.ConnectionString;
    }

    /// <summary>The Migrations host's wiring (Program.cs) with a logger that keeps every entry.</summary>
    private static async Task<int> RunAsync(string connectionString, CapturingLoggerProvider logs)
    {
        var mediaRoot = Path.Combine(Path.GetTempPath(), "freshorbit-migrate-tests", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{FreshOrbitDbContextOptions.ConnectionStringName}"] = connectionString,
                ["FileStorage:RootPath"] = mediaRoot,
            })
            .Build();
        var environment = new HostingEnvironment { EnvironmentName = Environments.Production, ApplicationName = "FreshOrbit.Migrations", ContentRootPath = mediaRoot, ContentRootFileProvider = new NullFileProvider() };
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information).AddProvider(logs));
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton<ICurrentActor>(SystemActor.Instance);
        services.AddFreshOrbitPersistence();
        services.AddFreshOrbitModules(configuration, Modules.All);
        services.AddSingleton<MigrationRunner>();
        await using var provider = services.BuildServiceProvider();
        try
        {
            return await provider.GetRequiredService<MigrationRunner>().RunAsync(new MigrationCommand(LoadSample: false, PurgeSample: false, CreateAdmin: null), Ct);
        }
        finally
        {
            if (Directory.Exists(mediaRoot))
            {
                Directory.Delete(mediaRoot, recursive: true);
            }
        }
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyCollection<LogEntry> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception)));
        }
    }
}
