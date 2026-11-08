using AwesomeAssertions;
using FreshOrbit.Payments.Domain.Webhooks;
using FreshOrbit.Payments.Infrastructure;
using FreshOrbit.Payments.Infrastructure.Jobs;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Payments.Tests.Integration;

/// <summary>SEC-13 retention purge of webhook bodies (D335).</summary>
public sealed class WebhookRetentionTests(PaymentsDatabase database)
{
    private const string Body = """{"contact":"+919800000000","vpa":"someone@upi"}""";
    private const string Headers = """{"x-signature":"abc"}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Finished_events_older_than_30_days_lose_body_and_headers_but_keep_their_summary()
    {
        var now = DateTimeOffset.UtcNow;
        var old = now.AddDays(-31);
        var processed = Received(old);
        processed.MarkProcessed(old);
        var ignored = Received(old);
        ignored.MarkIgnored(old);
        var rejected = WebhookEvent.Rejected("FAKE", NewEventId(), "payment.captured", "\"tampered\"", new string('b', 64), false, Headers, old);
        var error = Received(old);
        error.MarkError("UNKNOWN_PAYMENT", old);
        var pending = Received(old);
        var recent = Received(now.AddDays(-29));
        recent.MarkProcessed(now.AddDays(-29));
        WebhookEvent[] all = [processed, ignored, rejected, error, pending, recent];
        await using (var db = database.CreateDbContext())
        {
            db.AddRange(all);
            await db.SaveChangesAsync(Ct);
        }

        await RunAsync(now);
        await RunAsync(now); // a second run changes nothing

        await using var read = database.CreateDbContext();
        var ids = all.Select(e => e.Id).ToList();
        var rows = await read.Set<WebhookEvent>().AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, Ct);
        rows.Should().HaveCount(all.Length);
        foreach (var purged in new[] { processed, ignored, rejected })
        {
            rows[purged.Id].Payload.Should().Be(WebhookEvent.PurgedPayload);
            rows[purged.Id].Headers.Should().Be(WebhookEvent.PurgedHeaders);
            rows[purged.Id].PayloadSha256.Should().Be(purged.PayloadSha256);
            rows[purged.Id].Status.Should().Be(purged.Status);
            rows[purged.Id].ProviderEventId.Should().Be(purged.ProviderEventId);
        }

        foreach (var kept in new[] { error, pending, recent })
        {
            rows[kept.Id].Payload.Should().NotBe(WebhookEvent.PurgedPayload);
            rows[kept.Id].Headers.Should().NotBe(WebhookEvent.PurgedHeaders);
        }
    }

    [Fact]
    public void The_purge_is_registered_as_a_daily_recurring_job()
    {
        var services = new ServiceCollection();
        new PaymentsModule().Register(services, new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var job = provider.GetServices<IRecurringJobRegistration>().SelectMany(r => r.GetJobs()).Single(j => j.Id == WebhookPayloadRetentionJob.JobId);

        job.Cron.Should().Be("40 2 * * *");
        job.JobType.Should().Be<WebhookPayloadRetentionJob>();
        services.Should().Contain(d => d.ServiceType == typeof(WebhookPayloadRetentionJob) && d.Lifetime == ServiceLifetime.Scoped);
    }

    private async Task RunAsync(DateTimeOffset now)
    {
        await using var db = database.CreateDbContext();
        await new WebhookPayloadRetentionJob(db, new TestClock(now)).ExecuteAsync(Ct);
    }

    private static WebhookEvent Received(DateTimeOffset at)
        => WebhookEvent.Received("FAKE", NewEventId(), "payment.captured", Body, new string('a', 64), Headers, at);

    private static string NewEventId() => $"evt_retention_{Guid.NewGuid():N}";
}
