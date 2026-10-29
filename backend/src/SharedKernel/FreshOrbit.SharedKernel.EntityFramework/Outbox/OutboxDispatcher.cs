using System.Security.Cryptography;
using System.Text.Json;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Events;
using FreshOrbit.SharedKernel.Serialization;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreshOrbit.SharedKernel.EntityFramework.Outbox;

/// <summary>
/// Delivers outbox rows to their <see cref="IEventHandler{TEvent}"/>s.
/// <list type="number">
/// <item>Claims up to <see cref="OutboxOptions.BatchSize"/> unprocessed rows that are due (<c>next_attempt_at</c> null or
/// passed) with <c>FOR UPDATE SKIP LOCKED</c> inside a transaction, so several Workers never deliver the same row concurrently.</item>
/// <item>For each handler without an inbox row it opens a fresh DI scope, runs the handler and saves the
/// handler's tracked changes plus the <c>platform.inbox</c> row in one <c>SaveChanges</c> (exactly-once effect).</item>
/// <item>When every handler succeeded the row is marked processed; otherwise <c>attempts</c> is incremented, the
/// error kept and <c>next_attempt_at</c> set by <see cref="OutboxRetrySchedule"/> (exponential backoff with jitter, D296).
/// At <see cref="OutboxOptions.MaxAttempts"/> each failing handler gets a <c>platform.dead_letters</c> row, a Critical
/// log line is written and the outbox row is no longer picked until staff replay it.</item>
/// </list>
/// </summary>
public sealed partial class OutboxDispatcher
{
    private const int MaxErrorLength = 4000;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(IServiceScopeFactory scopeFactory, IClock clock, IOptions<OutboxOptions> options, ILogger<OutboxDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _scopeFactory = scopeFactory;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Processes one batch; returns the number of outbox rows claimed.</summary>
    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FreshOrbitDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var due = _clock.UtcNow;
        var messages = await db.Set<OutboxMessage>()
            .FromSql($"""
                SELECT * FROM platform.outbox
                WHERE processed_at IS NULL AND attempts < {_options.MaxAttempts}
                  AND (next_attempt_at IS NULL OR next_attempt_at <= {due})
                ORDER BY occurred_at, id
                LIMIT {_options.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);
        if (messages.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var eventIds = messages.Select(m => m.Id).ToList();
        var handled = (await db.Set<InboxRecord>()
                .Where(i => eventIds.Contains(i.EventId))
                .Select(i => new { i.EventId, i.Handler })
                .ToListAsync(cancellationToken))
            .Select(i => (i.EventId, i.Handler))
            .ToHashSet();

        foreach (var message in messages)
        {
            var failures = await DispatchAsync(message, handled, scope.ServiceProvider, cancellationToken);
            var now = _clock.UtcNow;
            if (failures.Count == 0)
            {
                message.ProcessedAt = now;
                message.NextAttemptAt = null;
                message.Error = null;
                continue;
            }

            message.Attempts++;
            message.Error = Truncate(string.Join(Environment.NewLine, failures.Select(f => $"{f.Handler}: {f.Error}")));
            if (message.Attempts >= _options.MaxAttempts)
            {
                message.NextAttemptAt = null;
                foreach (var (handler, error) in failures)
                {
                    db.Add(new DeadLetter(message.Id, handler, error, now));
                }

                if (_logger.IsEnabled(LogLevel.Critical))
                {
                    LogDeadLettered(message.Id, message.EventType, message.Attempts, string.Join(", ", failures.Select(f => f.Handler)));
                }
            }
            else
            {
                message.NextAttemptAt = now + OutboxRetrySchedule.DelayAfter(message.Attempts, _options.RetryBaseDelay, _options.RetryMaxDelay, JitterSample());
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages.Count;
    }

    private async Task<List<(string Handler, string Error)>> DispatchAsync(
        OutboxMessage message,
        HashSet<(Guid EventId, string Handler)> handled,
        IServiceProvider batchServices,
        CancellationToken cancellationToken)
    {
        var eventType = EventTypeNames.Resolve(message.EventType);
        if (eventType is null)
        {
            return [("(dispatcher)", $"Event type '{message.EventType}' cannot be resolved.")];
        }

        DomainEvent domainEvent;
        try
        {
            domainEvent = (DomainEvent)JsonSerializer.Deserialize(message.Payload, eventType, FreshOrbitJson.Options)!;
        }
        catch (JsonException exception)
        {
            return [("(dispatcher)", $"Payload cannot be deserialized: {exception.Message}")];
        }

        var invoker = EventHandlerInvoker.For(eventType);
        var handlerTypes = invoker.ResolveHandlers(batchServices).Select(h => h.GetType()).ToList();
        var failures = new List<(string Handler, string Error)>();
        foreach (var handlerType in handlerTypes)
        {
            var handlerName = handlerType.FullName ?? handlerType.Name;
            if (handled.Contains((message.Id, handlerName)))
            {
                continue;
            }

            try
            {
                await using var handlerScope = _scopeFactory.CreateAsyncScope();
                var handler = invoker.ResolveHandlers(handlerScope.ServiceProvider).First(h => h.GetType() == handlerType);
                await invoker.InvokeAsync(handler, domainEvent, cancellationToken);
                var handlerDb = handlerScope.ServiceProvider.GetRequiredService<FreshOrbitDbContext>();
                handlerDb.Add(new InboxRecord(message.Id, handlerName, _clock.UtcNow));
                await handlerDb.SaveChangesAsync(cancellationToken);
                handled.Add((message.Id, handlerName));
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                LogHandlerFailed(exception, handlerName, message.Id);
                failures.Add((handlerName, Truncate(exception.ToString())));
            }
        }

        return failures;
    }

    /// <summary>A uniform sample in [0, 1] for the retry jitter.</summary>
    private static double JitterSample() => RandomNumberGenerator.GetInt32(0, 1_000_001) / 1_000_000d;

    private static string Truncate(string value) => value.Length <= MaxErrorLength ? value : value[..MaxErrorLength];

    [LoggerMessage(Level = LogLevel.Warning, Message = "Event handler {Handler} failed for event {EventId}")]
    private partial void LogHandlerFailed(Exception exception, string handler, Guid eventId);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Event {EventId} ({EventType}) dead-lettered after {Attempts} attempts; failing handlers: {Handlers}. Replay it with the admin dead-letter replay once the cause is fixed")]
    private partial void LogDeadLettered(Guid eventId, string eventType, int attempts, string handlers);
}
