using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Migrations;

/// <summary>Applies migrations, loads reference data, then optionally purges and/or loads sample data.</summary>
internal sealed partial class MigrationRunner(IServiceScopeFactory scopeFactory, IConfiguration configuration, IHostEnvironment environment, ILogger<MigrationRunner> logger)
{
    public async Task<int> RunAsync(MigrationCommand command, CancellationToken cancellationToken)
    {
        if (command.LoadSample && environment.IsProduction())
        {
            LogSampleRefused(environment.EnvironmentName);
            return 2;
        }

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FreshOrbitDbContext>();
            LogStep("Applying migrations");
            await db.Database.MigrateAsync(cancellationToken);
        }

        var referencePath = DataDirectories.Resolve(configuration, "Seeding:ReferenceDataPath", "reference");
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            foreach (var loader in scope.ServiceProvider.GetServices<IReferenceDataLoader>().OrderBy(l => l.Order))
            {
                LogReferenceLoader(loader.GetType().Name);
                await loader.LoadAsync(new ReferenceDataContext(referencePath), cancellationToken);
            }
        }

        if (command.PurgeSample)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var deleted = await scope.ServiceProvider.GetRequiredService<SampleDataPurger>().PurgeAsync(cancellationToken);
            LogPurged(deleted);
        }

        if (command.LoadSample)
        {
            var samplePath = DataDirectories.Resolve(configuration, "Seeding:SampleDataPath", "sample");
            await using var scope = scopeFactory.CreateAsyncScope();
            var tracker = scope.ServiceProvider.GetRequiredService<ISampleRecordTracker>();
            foreach (var loader in scope.ServiceProvider.GetServices<ISampleDataLoader>().OrderBy(l => l.Order))
            {
                LogSampleLoader(loader.GetType().Name);
                await loader.LoadAsync(new SampleDataContext(samplePath, tracker), cancellationToken);
            }
        }

        LogStep("Done");
        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Step}")]
    private partial void LogStep(string step);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reference data: {Loader}")]
    private partial void LogReferenceLoader(string loader);

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} sample rows")]
    private partial void LogPurged(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample data: {Loader}")]
    private partial void LogSampleLoader(string loader);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sample data is refused in the {Environment} environment")]
    private partial void LogSampleRefused(string environment);
}

/// <summary>Command line: <c>[--sample] [--purge-sample]</c>.</summary>
internal sealed record MigrationCommand(bool LoadSample, bool PurgeSample)
{
    public static MigrationCommand? Parse(IReadOnlyCollection<string> args)
    {
        var known = new HashSet<string>(StringComparer.Ordinal) { "--sample", "--purge-sample" };
        var flags = args.Where(a => a.StartsWith("--", StringComparison.Ordinal) && !a.Contains('=', StringComparison.Ordinal)).ToList();
        return flags.Any(f => !known.Contains(f))
            ? null
            : new MigrationCommand(flags.Contains("--sample"), flags.Contains("--purge-sample"));
    }
}
