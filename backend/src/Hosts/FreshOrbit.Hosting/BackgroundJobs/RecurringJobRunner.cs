using System.Diagnostics;
using FreshOrbit.Hosting.Telemetry;
using FreshOrbit.SharedKernel.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Hosting.BackgroundJobs;

/// <summary>
/// Hangfire entry point for module recurring jobs; Hangfire creates a DI scope per run. Each run is a span and is
/// counted in <see cref="FreshOrbitMetrics"/> (runs, failures, duration per job id, D333); a failure is re-thrown so
/// Hangfire records and retries it as before.
/// </summary>
public sealed class RecurringJobRunner(IEnumerable<IRecurringJobRegistration> registrations, IServiceProvider services, FreshOrbitMetrics metrics)
{
    public async Task RunAsync(string jobId, CancellationToken cancellationToken)
    {
        var definition = registrations.SelectMany(r => r.GetJobs()).FirstOrDefault(j => j.Id == jobId)
            ?? throw new InvalidOperationException($"Recurring job '{jobId}' is not registered by any module.");
        var job = (IRecurringJob)services.GetRequiredService(definition.JobType);
        using var activity = FreshOrbitMetrics.ActivitySource.StartActivity($"job {jobId}");
        activity?.SetTag("job.id", jobId);
        var started = Stopwatch.GetTimestamp();
        var failed = true;
        try
        {
            await job.ExecuteAsync(cancellationToken);
            failed = false;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
        finally
        {
            metrics.RecordJobRun(jobId, Stopwatch.GetElapsedTime(started), failed);
        }
    }
}
