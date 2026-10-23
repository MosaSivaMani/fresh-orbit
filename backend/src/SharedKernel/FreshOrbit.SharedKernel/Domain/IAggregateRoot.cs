using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.SharedKernel.Domain;

/// <summary>
/// Marker for aggregate roots: they carry a row version (Postgres <c>xmin</c>) and collect domain events
/// that the unit of work writes to the outbox in the same transaction.
/// </summary>
public interface IAggregateRoot
{
    /// <summary>Row version loaded from the database (Postgres <c>xmin</c>). Zero for a new aggregate.</summary>
    uint Version { get; }

    IReadOnlyCollection<DomainEvent> DomainEvents { get; }

    void ClearEvents();
}
