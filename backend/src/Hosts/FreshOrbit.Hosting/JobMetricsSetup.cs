using FreshOrbit.Hosting.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Hosting;

public static class JobMetricsSetup
{
    /// <summary>
    /// The instruments the background jobs report to (<see cref="FreshOrbitMetrics"/>, D333) and, only while telemetry
    /// is enabled (<see cref="TelemetrySetup.OtlpEndpointKey"/>, D332), the outbox sampler that feeds the outbox gauges.
    /// </summary>
    public static IServiceCollection AddFreshOrbitJobMetrics(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMetrics();
        services.TryAddSingleton<FreshOrbitMetrics>();
        if (TelemetrySetup.IsEnabled(configuration))
        {
            services.AddHostedService<OutboxMetricsSampler>();
        }

        return services;
    }
}
