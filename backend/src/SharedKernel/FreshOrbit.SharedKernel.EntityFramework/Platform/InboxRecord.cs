namespace FreshOrbit.SharedKernel.EntityFramework.Platform;

/// <summary>Marks that <see cref="Handler"/> has handled event <see cref="EventId"/>, so it never runs twice.</summary>
public sealed class InboxRecord
{
    public InboxRecord(Guid eventId, string handler, DateTimeOffset processedAt)
    {
        EventId = eventId;
        Handler = handler;
        ProcessedAt = processedAt;
    }

    public Guid EventId { get; private set; }

    public string Handler { get; private set; }

    public DateTimeOffset ProcessedAt { get; private set; }
}
