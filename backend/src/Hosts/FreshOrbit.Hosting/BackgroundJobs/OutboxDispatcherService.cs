using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Hosting.BackgroundJobs;

/// <summary>
/// Runs the outbox dispatcher in a loop: a full batch is followed immediately by the next one; otherwise the
/// service waits <see cref="OutboxOptions.PollInterval"/> (5 s by default). A loop is used instead of a Hangfire
/// recurring job because Hangfire schedules at minute granularity and polls every 15 s.
/// </summary>
internal sealed partial class OutboxDispatcherService(OutboxDispatcher dispatcher, IOptions<OutboxOptions> options, ILogger<OutboxDispatcherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = 0;
            try
            {
                claimed = await dispatcher.DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogBatchFailed(exception);
            }

            if (claimed < settings.BatchSize)
            {
                try
                {
                    await Task.Delay(settings.PollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox dispatch batch failed")]
    private partial void LogBatchFailed(Exception exception);
}
