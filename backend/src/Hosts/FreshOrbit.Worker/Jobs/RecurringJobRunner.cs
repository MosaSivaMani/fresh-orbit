using FreshOrbit.SharedKernel.Jobs;

namespace FreshOrbit.Worker.Jobs;

/// <summary>Hangfire entry point for module recurring jobs; Hangfire creates a DI scope per run.</summary>
public sealed class RecurringJobRunner(IEnumerable<IRecurringJobRegistration> registrations, IServiceProvider services)
{
    public Task RunAsync(string jobId, CancellationToken cancellationToken)
    {
        var definition = registrations.SelectMany(r => r.GetJobs()).FirstOrDefault(j => j.Id == jobId)
            ?? throw new InvalidOperationException($"Recurring job '{jobId}' is not registered by any module.");
        var job = (IRecurringJob)services.GetRequiredService(definition.JobType);
        return job.ExecuteAsync(cancellationToken);
    }
}
