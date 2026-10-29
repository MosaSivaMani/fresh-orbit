using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Events;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FreshOrbit.SharedKernel.EntityFramework;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FreshOrbitDbContext"/> (connection string <c>ConnectionStrings:FreshOrbit</c>, read when
    /// the context is first created) with every <see cref="IInterceptor"/> registered in DI, <see cref="IUnitOfWork"/>, the platform model, the outbox dispatcher and the
    /// sample-data helpers. The host must register an <c>ICurrentActor</c>.
    /// </summary>
    public static IServiceCollection AddFreshOrbitPersistence(this IServiceCollection services)
    {
        services.AddDbContext<FreshOrbitDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(FreshOrbitDbContextOptions.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{FreshOrbitDbContextOptions.ConnectionStringName}' is not configured.");
            }

            FreshOrbitDbContextOptions.Configure(options, connectionString);

            // Interceptors registered by modules (e.g. Audit's admin-write recorder). The options are built per
            // scope, so scoped interceptors see the current request.
            options.AddInterceptors(provider.GetServices<IInterceptor>());
        });
        services.TryAddScoped<IUnitOfWork>(provider => provider.GetRequiredService<FreshOrbitDbContext>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleModel, PlatformModuleModel>());
        services.TryAddSingleton<IClock, SystemClock>();
        // Every Outbox value is required (no defaults); the dispatcher hosts also validate at start-up (BackgroundJobsSetup).
        services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.SectionName);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OutboxOptions>, OutboxOptionsValidator>());
        services.TryAddSingleton<OutboxDispatcher>();
        services.TryAddScoped<DeadLetterStore>();
        services.TryAddScoped<ISampleRecordTracker, SampleRecordTracker>();
        services.TryAddScoped<SampleDataPurger>();
        return services;
    }

    /// <summary>Adds a module's persistence model.</summary>
    public static IServiceCollection AddModuleModel<TModel>(this IServiceCollection services)
        where TModel : class, IModuleModel
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleModel, TModel>());
        return services;
    }

    /// <summary>Registers an outbox event handler (scoped; one instance per delivery).</summary>
    public static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : DomainEvent
        where THandler : class, IEventHandler<TEvent>
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEventHandler<TEvent>, THandler>());
        return services;
    }
}
