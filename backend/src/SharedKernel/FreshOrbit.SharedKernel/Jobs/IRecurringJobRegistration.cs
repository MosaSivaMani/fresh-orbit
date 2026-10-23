namespace FreshOrbit.SharedKernel.Jobs;

/// <summary>
/// A module's recurring jobs. Register implementations in the module's DI registration; the Worker schedules
/// every <see cref="RecurringJobDefinition"/> in Hangfire at startup and runs the job in a fresh DI scope.
/// </summary>
public interface IRecurringJobRegistration
{
    IEnumerable<RecurringJobDefinition> GetJobs();
}

/// <summary>A job body. Resolved from DI (register it as scoped) for each run.</summary>
public interface IRecurringJob
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>
/// <paramref name="Id"/> is stable and unique (e.g. <c>delivery.generate-slots</c>); <paramref name="Cron"/> is a
/// 5-part cron expression in UTC; <paramref name="JobType"/> implements <see cref="IRecurringJob"/>.
/// </summary>
public sealed record RecurringJobDefinition(string Id, string Cron, Type JobType);
