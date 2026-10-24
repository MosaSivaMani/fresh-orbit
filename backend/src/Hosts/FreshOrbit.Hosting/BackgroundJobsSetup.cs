using FreshOrbit.Hosting.BackgroundJobs;
using FreshOrbit.SharedKernel.EntityFramework;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Hosting;

public static class BackgroundJobsSetup
{
    /// <summary>Optional <c>Hangfire:WorkerCount</c>; absent or 0 keeps Hangfire's default worker count.</summary>
    public const string WorkerCountKey = "Hangfire:WorkerCount";

    /// <summary>
    /// The background work of FreshOrbit.Worker: the Hangfire server on Postgres (schema <c>hangfire</c>), every
    /// module's recurring job (<see cref="RecurringJobScheduler"/>, UTC cron) and the outbox dispatcher loop. The
    /// Worker host calls this; the Api calls it only in the Demo environment with
    /// <c>Hosting:RunBackgroundJobsInApi</c> (<see cref="DemoSetup"/>). The host registers persistence, the modules
    /// and an <c>ICurrentActor</c> that is the system actor outside a request.
    /// </summary>
    public static IServiceCollection AddFreshOrbitBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddHangfire((provider, hangfire) => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(provider.GetRequiredService<IConfiguration>().GetConnectionString(FreshOrbitDbContextOptions.ConnectionStringName)),
                new PostgreSqlStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true }));

        var workerCount = configuration.GetValue<int?>(WorkerCountKey).GetValueOrDefault();
        if (workerCount > 0)
        {
            services.AddHangfireServer(options => options.WorkerCount = workerCount);
        }
        else
        {
            services.AddHangfireServer();
        }

        services.AddScoped<RecurringJobRunner>();
        services.AddHostedService<RecurringJobScheduler>();
        services.AddHostedService<OutboxDispatcherService>();
        return services;
    }
}
