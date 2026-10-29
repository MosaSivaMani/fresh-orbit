namespace FreshOrbit.SharedKernel.EntityFramework.Platform;

/// <summary>A domain event waiting for (or done with) dispatch. <see cref="Id"/> is the event id.</summary>
public sealed class OutboxMessage
{
    public const int MaxEventTypeLength = 300;

    public OutboxMessage(Guid id, string eventType, string payload, DateTimeOffset occurredAt, string? correlationId)
    {
        Id = id;
        EventType = eventType;
        Payload = payload;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    /// <summary>
    /// Earliest time of the next delivery attempt after a failure (exponential backoff with jitter, D296); null means
    /// due now (a new row, or a dead letter replayed by staff).
    /// </summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    public string? Error { get; set; }
}
