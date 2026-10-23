using FreshOrbit.Notifications.Application.Sending;
using FreshOrbit.SharedKernel.Jobs;

namespace FreshOrbit.Notifications.Infrastructure.Senders;

/// <summary><c>notifications.send</c>, every minute (NOT-R6): sends batches until none is left or the run is cancelled.</summary>
public sealed class SendNotificationsJob(MessageDispatcher dispatcher) : IRecurringJob
{
    public const string JobId = "notifications.send";

    /// <summary>Bounds one run so a provider outage cannot keep the job busy forever.</summary>
    private const int MaxBatchesPerRun = 20;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < MaxBatchesPerRun; i++)
        {
            if (await dispatcher.SendBatchAsync(cancellationToken) == 0)
            {
                return;
            }
        }
    }
}

public sealed class NotificationsJobs : IRecurringJobRegistration
{
    public IEnumerable<RecurringJobDefinition> GetJobs() => [new RecurringJobDefinition(SendNotificationsJob.JobId, "* * * * *", typeof(SendNotificationsJob))];
}
