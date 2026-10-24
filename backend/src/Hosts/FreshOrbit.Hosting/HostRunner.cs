using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Hosting;

public static class HostRunner
{
    /// <summary>
    /// Runs the host until shutdown. Configuration refused at start-up (rule 7) ends with one Critical log line and exit
    /// code 1 instead of an unhandled crash, so a container restart loop reads plainly (E2E-2).
    /// </summary>
    public static async Task<int> RunFreshOrbitAsync(this IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        try
        {
            try
            {
                await host.StartAsync();
            }
            catch (OptionsValidationException exception)
            {
                var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(HostRunner));
                if (logger.IsEnabled(LogLevel.Critical))
                {
                    logger.LogCritical("Start-up refused: {OptionsName} configuration is invalid: {Failures}", exception.OptionsName, exception.Failures);
                }

                return 1;
            }

            await host.WaitForShutdownAsync();
            return 0;
        }
        finally
        {
            if (host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                host.Dispose();
            }
        }
    }
}
