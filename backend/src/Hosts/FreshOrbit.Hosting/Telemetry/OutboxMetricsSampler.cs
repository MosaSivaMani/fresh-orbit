using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Hosting.Telemetry;

/// <summary>
/// Every <see cref="Interval"/>, reads the outbox queue depth, its oldest waiting row and the dead letters of the last
/// 24 hours (three small indexed reads) into <see cref="FreshOrbitMetrics"/>. Registered only when telemetry is enabled
/// (D332) in a host that runs the background jobs, so a host without an exporter never polls for it.
/// </summary>
internal sealed partial class OutboxMetricsSampler(
    IServiceScopeFactory scopes,
    FreshOrbitMetrics metrics,
    IClock clock,
    IOptions<OutboxOptions> outbox,
    ILogger<OutboxMetricsSampler> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await SampleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogSampleFailed(exception);
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    internal async Task SampleAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FreshOrbitDbContext>();
        var now = clock.UtcNow;
        var maxAttempts = outbox.Value.MaxAttempts;
        var waiting = db.Set<OutboxMessage>().AsNoTracking().Where(m => m.ProcessedAt == null && m.Attempts < maxAttempts);
        var pending = await waiting.LongCountAsync(cancellationToken);
        var oldest = await waiting.MinAsync(m => (DateTimeOffset?)m.OccurredAt, cancellationToken);
        var since = now.AddDays(-1);
        var deadLetters = await db.Set<DeadLetter>().AsNoTracking().LongCountAsync(d => d.FailedAt >= since, cancellationToken);
        metrics.SetOutboxSnapshot(pending, oldest is null ? TimeSpan.Zero : now - oldest.Value, deadLetters);
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox metrics sample failed")]
    private partial void LogSampleFailed(Exception exception);
}
