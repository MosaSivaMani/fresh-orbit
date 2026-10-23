using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.SharedKernel.Web.Modules;

public static class ModuleExtensions
{
    /// <summary>Registers each module's services and keeps the module instances for endpoint mapping.</summary>
    public static IServiceCollection AddFreshOrbitModules(this IServiceCollection services, IConfiguration configuration, IEnumerable<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            if (!names.Add(module.Name))
            {
                throw new InvalidOperationException($"Module '{module.Name}' is registered twice.");
            }

            services.AddSingleton(module);
            module.Register(services, configuration);
        }

        return services;
    }

    /// <summary>Maps the endpoints of every module registered in DI (including test modules).</summary>
    public static IEndpointRouteBuilder MapFreshOrbitModules(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        foreach (var module in endpoints.ServiceProvider.GetServices<IModule>())
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
