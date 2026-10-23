using AwesomeAssertions;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Tests.TestModule;
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
    public async Task Failed_handler_is_retried_without_rerunning_handlers_that_succeeded()
    {
        await using var services = CreateServices(s => s
            .AddEventHandler<WidgetRenamed, WidgetRenamedLogger>()
            .AddEventHandler<WidgetRenamed, FlakyRenamedHandler>());
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
            (await LogsAsync(db, widget.Id)).Should().Equal("renamed:flaky-renamed");

            await dispatcher.DispatchBatchAsync(Ct);

            var afterSecond = await db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.Id == renamedId, Ct);
            afterSecond.ProcessedAt.Should().NotBeNull();
            (await LogsAsync(db, widget.Id)).Should().BeEquivalentTo("renamed:flaky-renamed", "flaky-ok");
            (await db.Set<InboxRecord>().CountAsync(i => i.EventId == renamedId, Ct)).Should().Be(2);
        }
    }

    [Fact]
    public async Task Handler_failing_every_attempt_is_dead_lettered_after_five_attempts()
    {
        await using var services = CreateServices(s => s.AddEventHandler<WidgetExploded, ExplodingHandler>());
        var dispatcher = services.GetRequiredService<OutboxDispatcher>();
        var widget = Widget.Create("boom");
        widget.Explode();
        var eventId = widget.DomainEvents.OfType<WidgetExploded>().Single().EventId;
        await SaveAsync(widget);

        for (var attempt = 0; attempt < 6; attempt++)
        {
            await dispatcher.DispatchBatchAsync(Ct);
        }

        await using var db = database.CreateDbContext();
        var message = await db.Set<OutboxMessage>().SingleAsync(m => m.Id == eventId, Ct);
        message.Attempts.Should().Be(5);
        message.ProcessedAt.Should().BeNull();
        var deadLetter = await db.Set<DeadLetter>().SingleAsync(d => d.EventId == eventId, Ct);
        deadLetter.Handler.Should().Be(typeof(ExplodingHandler).FullName);
        deadLetter.Error.Should().Contain("Always fails.");
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

    private ServiceProvider CreateServices(Action<IServiceCollection> handlers)
        => database.CreateServices(services =>
        {
            services.Configure<OutboxOptions>(o => o.BatchSize = 1_000);
            handlers(services);
        });

    private async Task SaveAsync(Widget widget)
    {
        await using var db = database.CreateDbContext();
        db.Add(widget);
        await db.SaveChangesAsync(Ct);
    }

    private static Task<List<string>> LogsAsync(FreshOrbitDbContext db, Guid widgetId)
        => db.Set<WidgetLog>().AsNoTracking().Where(l => l.WidgetId == widgetId).OrderBy(l => l.Id).Select(l => l.Entry).ToListAsync(Ct);
}
