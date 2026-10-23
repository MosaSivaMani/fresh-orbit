using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.SharedKernel.Events;

/// <summary>
/// Something that happened in an aggregate. Raised by the aggregate, written to <c>platform.outbox</c> in the
/// same transaction and dispatched by the Worker to every <see cref="IEventHandler{TEvent}"/>.
/// Events are serialized with System.Text.Json, so keep them as flat sealed records of primitives.
/// </summary>
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = IdGenerator.NewId();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
