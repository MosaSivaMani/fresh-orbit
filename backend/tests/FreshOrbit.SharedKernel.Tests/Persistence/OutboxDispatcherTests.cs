using AwesomeAssertions;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Tests.TestModule;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.SharedKernel.Tests.Persistence;

public sealed class OutboxDispatcherTests(SharedKernelDatabase database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Dispatch_runs_each_handler_once_saves_its_changes_and_marks_the_row_processed()
    {
        await using var services = CreateServices(s => s.AddEventHandler<WidgetCreated, WidgetCreatedLogger>());
        var dispatcher = services.GetRequiredService<OutboxDispatcher>();
        var widget = Widget.Create("dispatch");
        var eventId = widget.DomainEvents.Single().EventId;
        await SaveAsync(widget);

        (await dispatcher.DispatchBatchAsync(Ct)).Should().BeGreaterThan(0);

        await using (var db = database.CreateDbContext())
        {
            var message = await db.Set<OutboxMessage>().SingleAsync(m => m.Id == eventId, Ct);
            message.ProcessedAt.Should().NotBeNull();
            message.Attempts.Should().Be(0);
            (await db.Set<InboxRecord>().CountAsync(i => i.EventId == eventId, Ct)).Should().Be(1);
            (await LogsAsync(db, widget.Id)).Should().Equal("created:dispatch");

            // Simulate a redelivery (e.g. the batch commit was lost): the inbox stops the handler running twice.
            await db.Database.ExecuteSqlAsync($"UPDATE platform.outbox SET processed_at = NULL WHERE id = {eventId}", Ct);
        }

        await dispatcher.DispatchBatchAsync(Ct);

        await using var after = database.CreateDbContext();
        (await LogsAsync(after, widget.Id)).Should().Equal("created:dispatch");
        (await after.Set<OutboxMessage>().SingleAsync(m => m.Id == eventId, Ct)).ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Failed_handler_is_retried_after_its_backoff_without_rerunning_handlers_that_succeeded()
    {
        var clock = new TestClock();
        await using var services = CreateServices(
            s => s
                .AddEventHandler<WidgetRenamed, WidgetRenamedLogger>()
                .AddEventHandler<WidgetRenamed, FlakyRenamedHandler>(),
            clock);
        var dispatcher = services.GetRequiredService<OutboxDispatcher>();
        var widget = Widget.Create("flaky");
        await SaveAsync(widget);
        await using (var db = database.CreateDbContext())
        {
            var loaded = await db.Set<Widget>().SingleAsync(w => w.Id == widget.Id, Ct);
            loaded.Rename("flaky-renamed");
            widget = loaded;
            var renamedId = loaded.DomainEvents.Single().EventId;
            await db.SaveChangesAsync(Ct);

            await dispatcher.DispatchBatchAsync(Ct);

            var afterFirst = await db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.Id == renamedId, Ct);
            afterFirst.ProcessedAt.Should().BeNull();
            afterFirst.Attempts.Should().Be(1);
            afterFirst.Error.Should().Contain(nameof(FlakyRenamedHandler));
            afterFirst.NextAttemptAt.Should().NotBeNull();
            (await LogsAsync(db, widget.Id)).Should().Equal("renamed:flaky-renamed");

            // BE-2: not retried before its backoff delay has passed.
            await dispatcher.DispatchBatchAsync(Ct);
            (await db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.Id == renamedId, Ct)).Attempts.Should().Be(1);

            clock.Set(afterFirst.NextAttemptAt!.Value);
            await dispatcher.DispatchBatchAsync(Ct);

            var afterSecond = await db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.Id == renamedId, Ct);
            afterSecond.ProcessedAt.Should().NotBeNull();
            afterSecond.NextAttemptAt.Should().BeNull();
            (await LogsAsync(db, widget.Id)).Should().BeEquivalentTo("renamed:flaky-renamed", "flaky-ok");
            (await db.Set<InboxRecord>().CountAsync(i => i.EventId == renamedId, Ct)).Should().Be(2);
        }
    }

    [Fact]
    public async Task Failed_row_is_rescheduled_with_exponential_backoff_and_jitter()
    {
        var clock = new TestClock();
        await using var services = CreateServices(s => s.AddEventHandler<WidgetExploded, ExplodingHandler>(), clock);
        var dispatcher = services.GetRequiredService<OutboxDispatcher>();
        var widget = Widget.Create("backoff");
        widget.Explode();
        var eventId = widget.DomainEvents.OfType<WidgetExploded>().Single().EventId;
        await SaveAsync(widget);

        var delays = new List<TimeSpan>();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var before = clock.UtcNow;
            await dispatcher.DispatchBatchAsync(Ct);
            await using var db = database.CreateDbContext();
            var message = await db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.Id == eventId, Ct);
            message.Attempts.Should().Be(attempt);
            message.ProcessedAt.Should().BeNull();
            message.NextAttemptAt.Should().NotBeNull();
            delays.Add(message.NextAttemptAt!.Value - before);
            clock.Set(message.NextAttemptAt.Value);
        }

        // 10 s, 20 s, 40 s (TestOutboxSettings), each within the ±20 % jitter.
        for (var i = 0; i < delays.Count; i++)
        {
            var exact = TestOutboxSettings.RetryBaseDelay * Math.Pow(2, i);
            var tolerance = TimeSpan.FromMilliseconds(1);
            delays[i].Should().BeGreaterThanOrEqualTo((exact * 0.8) - tolerance).And.BeLessThanOrEqualTo((exact * 1.2) + tolerance);
        }

        await using var read = database.CreateDbContext();
        (await read.Set<DeadLetter>().CountAsync(d => d.EventId == eventId, Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Handler_failing_every_attempt_is_dead_lettered_after_the_configured_attempts()
    {
        var clock = new TestClock();
        await using var services = CreateServices(s => s.AddEventHandler<WidgetExploded, ExplodingHandler>(), clock);
        var dispatcher = services.GetRequiredService<OutboxDispatcher>();
        var widget = Widget.Create("boom");
        widget.Explode();
        var eventId = widget.DomainEvents.OfType<WidgetExploded>().Single().EventId;
        await SaveAsync(widget);

        for (var attempt = 0; attempt < TestOutboxSettings.MaxAttempts + 1; attempt++)
        {
            await dispatcher.DispatchBatchAsync(Ct);
            clock.Advance(TestOutboxSettings.RetryMaxDelay * 2);
        }

        await using var db = database.CreateDbContext();
        var message = await db.Set<OutboxMessage>().SingleAsync(m => m.Id == eventId, Ct);
        message.Attempts.Should().Be(TestOutboxSettings.MaxAttempts);
        message.ProcessedAt.Should().BeNull();
        message.NextAttemptAt.Should().BeNull();
        var deadLetter = await db.Set<DeadLetter>().SingleAsync(d => d.EventId == eventId, Ct);
        deadLetter.Handler.Should().Be(typeof(ExplodingHandler).FullName);
        deadLetter.Error.Should().Contain("Always fails.");
        deadLetter.ReplayedAt.Should().BeNull();
    }

    [Fact]
    public async Task Unresolvable_event_type_counts_as_a_failed_attempt()
    {
        await using var services = CreateServices(_ => { });
        var dispatcher = services.GetRequiredService<OutboxDispatcher>();
        var id = Guid.CreateVersion7();
        await using (var db = database.CreateDbContext())
        {
            db.Add(new OutboxMessage(id, "No.Such.Event, NoSuchAssembly", "{}", DateTimeOffset.UtcNow, null));
            await db.SaveChangesAsync(Ct);
        }

        await dispatcher.DispatchBatchAsync(Ct);

        await using var read = database.CreateDbContext();
        var message = await read.Set<OutboxMessage>().SingleAsync(m => m.Id == id, Ct);
        message.Attempts.Should().Be(1);
        message.Error.Should().Contain("cannot be resolved");
    }

    private ServiceProvider CreateServices(Action<IServiceCollection> handlers, TestClock? clock = null)
        => database.CreateServices(
            services =>
            {
                services.Configure<OutboxOptions>(o => o.BatchSize = 1_000);
                handlers(services);
            },
            clock);

    private async Task SaveAsync(Widget widget)
    {
        await using var db = database.CreateDbContext();
        db.Add(widget);
        await db.SaveChangesAsync(Ct);
    }

    private static Task<List<string>> LogsAsync(FreshOrbitDbContext db, Guid widgetId)
        => db.Set<WidgetLog>().AsNoTracking().Where(l => l.WidgetId == widgetId).OrderBy(l => l.Id).Select(l => l.Entry).ToListAsync(Ct);
}
