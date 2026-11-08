using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.SharedKernel.EntityFramework.Outbox;

/// <summary>One dead letter with its event (list and replay responses, D296).</summary>
public sealed record DeadLetterView(
    Guid Id,
    Guid EventId,
    string EventType,
    string Handler,
    string Error,
    DateTimeOffset FailedAt,
    int Attempts,
    DateTimeOffset? ReplayedAt,
    Guid? ReplayedBy);

/// <summary>Status filter of the dead-letter list: <c>OPEN</c> (not replayed yet) or <c>REPLAYED</c>.</summary>
public enum DeadLetterStatus
{
    Open,
    Replayed,
}

/// <summary>
/// Staff access to <c>platform.dead_letters</c> (BE-2, D296). The platform tables stay written only by this outbox code
/// (rule 3); the Audit module maps the admin endpoints and saves. <see cref="ReplayAsync"/> tracks its changes on the
/// shared context and never saves, so the caller commits the replay and its audit entry in one <c>SaveChanges</c>.
/// </summary>
public sealed class DeadLetterStore(FreshOrbitDbContext db, IClock clock)
{
    public const string NotFoundCode = "DEAD_LETTER_NOT_FOUND";
    public const string AlreadyReplayedCode = "DEAD_LETTER_ALREADY_REPLAYED";

    /// <summary>Dead letters newest first, with their event type and attempts.</summary>
    public async Task<(IReadOnlyList<DeadLetterView> Items, int TotalCount)> ListAsync(DeadLetterStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = db.Set<DeadLetter>().AsNoTracking();
        query = status switch
        {
            DeadLetterStatus.Open => query.Where(d => d.ReplayedAt == null),
            DeadLetterStatus.Replayed => query.Where(d => d.ReplayedAt != null),
            _ => query,
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(d => d.FailedAt).ThenBy(d => d.Id)
            .Skip(skip).Take(take)
            .Join(
                db.Set<OutboxMessage>().AsNoTracking(),
                d => d.EventId,
                m => m.Id,
                (d, m) => new DeadLetterView(d.Id, d.EventId, m.EventType, d.Handler, d.Error, d.FailedAt, m.Attempts, d.ReplayedAt, d.ReplayedBy))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    /// <summary>
    /// Puts the event of <paramref name="deadLetterId"/> back in the queue: the outbox row gets attempts 0 and is due now,
    /// and every open dead letter of the event is marked replayed (the dispatcher runs only the handlers that have no
    /// inbox row, so handlers that succeeded are not run again). A handler that fails again gets a new dead letter after
    /// the full backoff schedule. Tracked, not saved.
    /// </summary>
    public async Task<DeadLetterView> ReplayAsync(Guid deadLetterId, Guid? replayedBy, CancellationToken cancellationToken)
    {
        var deadLetter = await db.Set<DeadLetter>().FirstOrDefaultAsync(d => d.Id == deadLetterId, cancellationToken)
            ?? throw new NotFoundException(NotFoundCode, "The dead letter does not exist; check the deadLetterId.");
        var message = await db.Set<OutboxMessage>().FirstOrDefaultAsync(m => m.Id == deadLetter.EventId, cancellationToken)
            ?? throw new NotFoundException(NotFoundCode, "The event of this dead letter no longer exists; check the deadLetterId.");
        if (deadLetter.ReplayedAt is not null || message.ProcessedAt is not null)
        {
            throw new ConflictException(AlreadyReplayedCode, "This dead letter was already replayed; check the deadLetterId.", "deadLetterId");
        }

        var now = clock.UtcNow;
        var open = await db.Set<DeadLetter>().Where(d => d.EventId == message.Id && d.ReplayedAt == null).ToListAsync(cancellationToken);
        foreach (var letter in open)
        {
            letter.MarkReplayed(now, replayedBy);
        }

        message.Attempts = 0;
        message.NextAttemptAt = null;
        return new DeadLetterView(deadLetter.Id, deadLetter.EventId, message.EventType, deadLetter.Handler, deadLetter.Error, deadLetter.FailedAt, message.Attempts, deadLetter.ReplayedAt, deadLetter.ReplayedBy);
    }
}
