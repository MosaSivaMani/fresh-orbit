using FreshOrbit.Identity.Application.Bootstrap;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Migrations;

/// <summary>
/// Applies migrations, then every module's <see cref="IModuleModel.DatabaseObjectsSql"/> (views, functions, exclusion
/// constraints; idempotent, after all tables exist), loads reference data, optionally creates the first admin, then
/// optionally purges and/or loads sample data.
/// </summary>
internal sealed partial class MigrationRunner(IServiceScopeFactory scopeFactory, IConfiguration configuration, IHostEnvironment environment, ILogger<MigrationRunner> logger)
{
    public async Task<int> RunAsync(MigrationCommand command, CancellationToken cancellationToken)
    {
        if ((command.LoadSample || command.PurgeSample) && environment.IsProduction())
        {
            LogSampleRefused(environment.EnvironmentName);
            return 2;
        }

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FreshOrbitDbContext>();
            LogStep("Applying migrations");
            await db.Database.MigrateAsync(cancellationToken);
            LogStep("Creating database objects");
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var sql in db.ModuleModels.SelectMany(model => model.DatabaseObjectsSql))
            {
                await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
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

        if (command.CreateAdmin is { } admin)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            try
            {
                var staffMemberId = await scope.ServiceProvider.GetRequiredService<IFirstAdminService>()
                    .CreateFirstAdminAsync(new CreateFirstAdminCommand(admin.Email, admin.FullName, admin.Password), cancellationToken);
                LogAdminCreated(admin.Email, staffMemberId);
            }
            catch (DomainException exception)
            {
                var detail = exception is RefusalException refusal
                    ? string.Join("; ", refusal.Errors.Select(e => $"{e.Field}: {e.Message}"))
                    : exception.Message;
                LogAdminRefused(exception.Code, detail);
                return 3;
            }
        }

        if (command.PurgeSample)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            try
            {
                var deleted = await scope.ServiceProvider.GetRequiredService<SampleDataPurger>().PurgeAsync(cancellationToken);
                LogPurged(deleted);
            }
            catch (SamplePurgeRefusedException exception)
            {
                LogPurgeRefused(exception.Message);
                return 4;
            }
        }

        if (command.LoadSample)
        {
            var samplePath = DataDirectories.Resolve(configuration, "Seeding:SampleDataPath", "sample");
            await using var scope = scopeFactory.CreateAsyncScope();
            var tracker = scope.ServiceProvider.GetRequiredService<ISampleRecordTracker>();
            var context = new SampleDataContext(samplePath, tracker, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
            foreach (var loader in scope.ServiceProvider.GetServices<ISampleDataLoader>().OrderBy(l => l.Order))
            {
                LogSampleLoader(loader.GetType().Name);
                await loader.LoadAsync(context, cancellationToken);
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

    [LoggerMessage(Level = LogLevel.Error, Message = "{Reason}")]
    private partial void LogPurgeRefused(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample data: {Loader}")]
    private partial void LogSampleLoader(string loader);

    [LoggerMessage(Level = LogLevel.Information, Message = "First admin {Email} created (staff member {StaffMemberId})")]
    private partial void LogAdminCreated(string email, Guid staffMemberId);

    [LoggerMessage(Level = LogLevel.Error, Message = "First admin refused: {Code} {Detail}")]
    private partial void LogAdminRefused(string code, string detail);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sample data (--sample, --purge-sample) is refused in the {Environment} environment")]
    private partial void LogSampleRefused(string environment);
}

/// <summary>Command line: <c>[--sample] [--purge-sample] [--create-admin --email E --name N --password P]</c>.</summary>
internal sealed record MigrationCommand(bool LoadSample, bool PurgeSample, FirstAdmin? CreateAdmin)
{
    public const string Usage = "Usage: FreshOrbit.Migrations [--sample] [--purge-sample] [--create-admin --email <email> --name <full name> --password <password>]";

    private static readonly HashSet<string> Flags = new(StringComparer.Ordinal) { "--sample", "--purge-sample", "--create-admin" };

    private static readonly HashSet<string> Values = new(StringComparer.Ordinal) { "--email", "--name", "--password" };

    /// <summary>Returns null when an option is unknown, a value is missing, or <c>--create-admin</c> lacks a value.</summary>
    public static MigrationCommand? Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal) || arg.Contains('=', StringComparison.Ordinal))
            {
                continue; // configuration overrides (--Key=Value) are for the host builder
            }

            if (Flags.Contains(arg))
            {
                flags.Add(arg);
            }
            else if (Values.Contains(arg) && i + 1 < args.Count)
            {
                values[arg] = args[++i];
            }
            else
            {
                return null;
            }
        }

        FirstAdmin? admin = null;
        if (flags.Contains("--create-admin"))
        {
            if (!values.TryGetValue("--email", out var email) || !values.TryGetValue("--name", out var name) || !values.TryGetValue("--password", out var password))
            {
                return null;
            }

            admin = new FirstAdmin(email, name, password);
        }
        else if (values.Count > 0)
        {
            return null;
        }

        return new MigrationCommand(flags.Contains("--sample"), flags.Contains("--purge-sample"), admin);
    }

    /// <summary>Configuration overrides (<c>--Key=Value</c>) only; the command's own options never reach configuration.</summary>
    public static string[] ConfigurationArgs(IEnumerable<string> args)
        => [.. args.Where(a => a.StartsWith("--", StringComparison.Ordinal) && a.Contains('=', StringComparison.Ordinal))];
}

internal sealed record FirstAdmin(string Email, string FullName, string Password)
{
    public override string ToString() => $"FirstAdmin {{ Email = {Email}, FullName = {FullName} }}";
}
