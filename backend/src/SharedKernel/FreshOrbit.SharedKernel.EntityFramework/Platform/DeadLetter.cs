using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.SharedKernel.EntityFramework.Platform;

/// <summary>An event a handler failed to process after the maximum number of attempts.</summary>
public sealed class DeadLetter
{
    public DeadLetter(Guid eventId, string handler, string error, DateTimeOffset failedAt)
    {
        Id = IdGenerator.NewId();
        EventId = eventId;
        Handler = handler;
        Error = error;
        FailedAt = failedAt;
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string Handler { get; private set; }

    public string Error { get; private set; }

    public DateTimeOffset FailedAt { get; private set; }

    /// <summary>When staff replayed the event of this dead letter (D296); null while it is open.</summary>
    public DateTimeOffset? ReplayedAt { get; private set; }

    /// <summary>The user id of the staff member who replayed it.</summary>
    public Guid? ReplayedBy { get; private set; }

    public void MarkReplayed(DateTimeOffset at, Guid? by)
    {
        ReplayedAt = at;
        ReplayedBy = by;
    }
}
