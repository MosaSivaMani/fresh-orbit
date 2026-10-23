using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Audit.Domain.Entries;

/// <summary>One append-only audit row (AUD-R1–R4). The id is a database identity.</summary>
public sealed class AuditEntry : Entity<long>
{
    public const int MaxEntityTypeLength = 60;
    public const int MaxEntityIdLength = 100;

    private AuditEntry()
    {
        Action = EntityType = EntityId = string.Empty;
    }

    public DateTimeOffset OccurredAt { get; private set; }

    public ActorType ActorType { get; private set; }

    public Guid? ActorId { get; private set; }

    public string? ActorName { get; private set; }

    public string Action { get; private set; }

    public string EntityType { get; private set; }

    public string EntityId { get; private set; }

    /// <summary>JSON document (jsonb) of the state before the change.</summary>
    public string? Before { get; private set; }

    /// <summary>JSON document (jsonb) of the state after the change.</summary>
    public string? After { get; private set; }

    public string? Ip { get; private set; }

    public string? CorrelationId { get; private set; }

    public static AuditEntry Create(AuditEntryData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ErrorCodes.EnsureValid(data.Action);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.EntityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.EntityId);
        if (data.EntityType.Length > MaxEntityTypeLength || data.EntityId.Length > MaxEntityIdLength)
        {
            throw new ArgumentException("Audit entity type or id is too long.", nameof(data));
        }

        return new AuditEntry
        {
            OccurredAt = data.OccurredAt,
            ActorType = data.ActorType,
            ActorId = data.ActorId,
            ActorName = data.ActorName,
            Action = data.Action,
            EntityType = data.EntityType,
            EntityId = data.EntityId,
            Before = data.Before,
            After = data.After,
            Ip = data.Ip,
            CorrelationId = data.CorrelationId,
        };
    }
}

public sealed record AuditEntryData(
    DateTimeOffset OccurredAt,
    ActorType ActorType,
    Guid? ActorId,
    string? ActorName,
    string Action,
    string EntityType,
    string EntityId,
    string? Before,
    string? After,
    string? Ip,
    string? CorrelationId);
