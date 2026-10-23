using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Compact;

namespace FreshOrbit.Hosting;

public static class LoggingSetup
{
    /// <summary>Serilog to the console: readable text in Development, compact JSON elsewhere. Levels from <c>Serilog</c> configuration.</summary>
    public static IServiceCollection AddFreshOrbitSerilog(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return services.AddSerilog((provider, logger) =>
        {
            logger.ReadFrom.Configuration(configuration)
                .ReadFrom.Services(provider)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", environment.ApplicationName);
            if (environment.IsDevelopment())
            {
                logger.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
        });
    }
}
