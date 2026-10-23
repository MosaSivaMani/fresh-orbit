using System.Collections.Concurrent;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.SharedKernel.Tests.TestModule;

public sealed class WidgetCreatedLogger(FreshOrbitDbContext db) : IEventHandler<WidgetCreated>
{
    public Task HandleAsync(WidgetCreated domainEvent, CancellationToken cancellationToken)
    {
        db.Add(new WidgetLog(domainEvent.WidgetId, $"created:{domainEvent.Name}"));
        return Task.CompletedTask;
    }
}

public sealed class WidgetRenamedLogger(FreshOrbitDbContext db) : IEventHandler<WidgetRenamed>
{
    public Task HandleAsync(WidgetRenamed domainEvent, CancellationToken cancellationToken)
    {
        db.Add(new WidgetLog(domainEvent.WidgetId, $"renamed:{domainEvent.Name}"));
        return Task.CompletedTask;
    }
}

/// <summary>Fails the first time it sees each event, then succeeds.</summary>
public sealed class FlakyRenamedHandler(FreshOrbitDbContext db) : IEventHandler<WidgetRenamed>
{
    private static readonly ConcurrentDictionary<Guid, int> Calls = new();

    public Task HandleAsync(WidgetRenamed domainEvent, CancellationToken cancellationToken)
    {
        if (Calls.AddOrUpdate(domainEvent.EventId, 1, (_, n) => n + 1) == 1)
        {
            db.Add(new WidgetLog(domainEvent.WidgetId, "flaky-should-not-be-saved"));
            throw new InvalidOperationException("Transient failure.");
        }

        db.Add(new WidgetLog(domainEvent.WidgetId, "flaky-ok"));
        return Task.CompletedTask;
    }
}

public sealed class ExplodingHandler : IEventHandler<WidgetExploded>
{
    public Task HandleAsync(WidgetExploded domainEvent, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Always fails.");
}
