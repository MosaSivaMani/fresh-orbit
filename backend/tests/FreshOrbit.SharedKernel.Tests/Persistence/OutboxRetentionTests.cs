using AwesomeAssertions;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.SharedKernel.Tests.Persistence;

/// <summary>SEC-13 retention purge of processed outbox rows and their inbox rows (D336).</summary>
public sealed class OutboxRetentionTests(SharedKernelDatabase database)
{
    private const string EventType = "Retention.Test.Event, NoSuchAssembly";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Processed_rows_older_than_7_days_and_their_inbox_rows_are_deleted_waiting_rows_are_kept()
    {
        var now = DateTimeOffset.UtcNow;
        var oldProcessed = Message(now.AddDays(-9), processedAt: now.AddDays(-8));
        var recentProcessed = Message(now.AddDays(-7).AddHours(2), processedAt: now.AddDays(-6));
        // Waiting or dead-lettered: never processed, so never purged, however old (a dead letter stays replayable).
        var oldWaiting = Message(now.AddDays(-30), processedAt: null);
        await using (var db = database.CreateDbContext())
        {
            db.AddRange(oldProcessed, recentProcessed, oldWaiting);
            db.AddRange(
                new InboxRecord(oldProcessed.Id, "HandlerA", now.AddDays(-8)),
                new InboxRecord(recentProcessed.Id, "HandlerA", now.AddDays(-6)),
                new InboxRecord(oldWaiting.Id, "HandlerA", now.AddDays(-29)));
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = database.CreateDbContext())
        {
            await new OutboxRetentionJob(db, new TestClock(now)).ExecuteAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        Guid[] ids = [oldProcessed.Id, recentProcessed.Id, oldWaiting.Id];
        var outbox = await read.Set<OutboxMessage>().AsNoTracking().Where(m => ids.Contains(m.Id)).Select(m => m.Id).ToListAsync(Ct);
        var inbox = await read.Set<InboxRecord>().AsNoTracking().Where(i => ids.Contains(i.EventId)).Select(i => i.EventId).ToListAsync(Ct);
        outbox.Should().BeEquivalentTo([recentProcessed.Id, oldWaiting.Id]);
        inbox.Should().BeEquivalentTo([recentProcessed.Id, oldWaiting.Id]);

        // Leave no undeliverable row behind for the dispatcher tests.
        await read.Set<InboxRecord>().Where(i => ids.Contains(i.EventId)).ExecuteDeleteAsync(Ct);
        await read.Set<OutboxMessage>().Where(m => ids.Contains(m.Id)).ExecuteDeleteAsync(Ct);
    }

    [Fact]
    public void The_purge_is_registered_as_a_daily_recurring_job()
    {
        var services = new ServiceCollection();
        services.AddFreshOrbitPlatformJobs();
        using var provider = services.BuildServiceProvider();

        var job = provider.GetServices<IRecurringJobRegistration>().SelectMany(r => r.GetJobs()).Should().ContainSingle().Subject;

        job.Id.Should().Be(OutboxRetentionJob.JobId);
        job.Cron.Should().Be("30 2 * * *");
        job.JobType.Should().Be<OutboxRetentionJob>();
        services.Should().Contain(d => d.ServiceType == typeof(OutboxRetentionJob) && d.Lifetime == ServiceLifetime.Scoped);
    }

    private static OutboxMessage Message(DateTimeOffset occurredAt, DateTimeOffset? processedAt)
        => new(Guid.CreateVersion7(), EventType, "{}", occurredAt, null) { ProcessedAt = processedAt, Attempts = processedAt is null ? 1000 : 0 };
}
