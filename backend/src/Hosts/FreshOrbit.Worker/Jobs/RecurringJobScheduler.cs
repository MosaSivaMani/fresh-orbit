using FreshOrbit.SharedKernel.Jobs;
using Hangfire;

namespace FreshOrbit.Worker.Jobs;

/// <summary>At startup, adds or updates a Hangfire recurring job (UTC cron) for every job modules register.</summary>
internal sealed class RecurringJobScheduler(IEnumerable<IRecurringJobRegistration> registrations, IRecurringJobManager manager) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var definitions = registrations.SelectMany(r => r.GetJobs()).ToList();
        var duplicate = definitions.GroupBy(d => d.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Recurring job id '{duplicate.Key}' is registered more than once.");
        }

        foreach (var definition in definitions)
        {
            if (!typeof(IRecurringJob).IsAssignableFrom(definition.JobType))
            {
                throw new InvalidOperationException($"{definition.JobType.Name} does not implement IRecurringJob.");
            }

            var jobId = definition.Id;
            manager.AddOrUpdate<RecurringJobRunner>(
                jobId,
                runner => runner.RunAsync(jobId, CancellationToken.None),
                definition.Cron,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
