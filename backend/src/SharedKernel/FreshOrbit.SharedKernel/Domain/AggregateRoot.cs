using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.SharedKernel.Domain;

/// <summary>Aggregate root with a key of type <typeparamref name="TId"/>.</summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    private readonly List<DomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    /// <inheritdoc />
    public uint Version { get; private set; }

    /// <inheritdoc />
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <inheritdoc />
    public void ClearEvents() => _domainEvents.Clear();

    protected void Raise(DomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}

/// <summary>Aggregate root with a UUIDv7 key generated on creation.</summary>
public abstract class AggregateRoot : AggregateRoot<Guid>
{
    protected AggregateRoot()
        : base(IdGenerator.NewId())
    {
    }

    protected AggregateRoot(Guid id)
        : base(id)
    {
    }
}
