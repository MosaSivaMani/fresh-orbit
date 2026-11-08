using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.SharedKernel.EntityFramework.Outbox;

/// <summary>
/// Daily retention purge of the <c>platform</c> outbox (SEC-13, D336). Event payloads carry personal data (phone
/// numbers, names, addresses in order events), so a delivered event is not kept for ever. Two set-based deletes in one
/// transaction, no rows loaded:
/// <list type="bullet">
/// <item><c>platform.outbox</c> rows processed more than <see cref="ProcessedRetention"/> ago. Rows still waiting or
/// dead-lettered (<c>processed_at</c> null) are never touched, so a dead letter stays replayable.</item>
/// <item><c>platform.inbox</c> rows older than the same period whose event is not still waiting in the outbox; the inbox
/// is only needed while its event can be delivered again.</item>
/// </list>
/// </summary>
public sealed class OutboxRetentionJob(FreshOrbitDbContext db, IClock clock) : IRecurringJob
{
    public const string JobId = "platform.purge-outbox";

    /// <summary>SEC-13: processed outbox rows are kept 7 days (enough to investigate a follow-on that went wrong).</summary>
    public static readonly TimeSpan ProcessedRetention = TimeSpan.FromDays(7);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.UtcNow - ProcessedRetention;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
        await db.Set<InboxRecord>()
            .Where(i => i.ProcessedAt < cutoff && !db.Set<OutboxMessage>().Any(m => m.Id == i.EventId && m.ProcessedAt == null))
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

/// <summary>The recurring jobs of the <c>platform</c> schema; the background-job hosts register it.</summary>
public sealed class PlatformJobRegistration : IRecurringJobRegistration
{
    public IEnumerable<RecurringJobDefinition> GetJobs() => [new(OutboxRetentionJob.JobId, "30 2 * * *", typeof(OutboxRetentionJob))];
}
