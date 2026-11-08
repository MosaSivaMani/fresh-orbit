using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using FreshOrbit.SharedKernel.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.SharedKernel.EntityFramework;

public static class PlatformJobsServiceCollectionExtensions
{
    /// <summary>
    /// The recurring jobs of the <c>platform</c> schema (today the outbox retention purge, D336). Only the hosts that run
    /// background jobs call this; modules register their own purges for their own tables.
    /// </summary>
    public static IServiceCollection AddFreshOrbitPlatformJobs(this IServiceCollection services)
    {
        services.TryAddScoped<OutboxRetentionJob>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRecurringJobRegistration, PlatformJobRegistration>());
        return services;
    }
}
