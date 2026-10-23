namespace FreshOrbit.SharedKernel.Domain;

/// <summary>Base class for entities identified by <typeparamref name="TId"/>.</summary>
public abstract class Entity<TId>
    where TId : notnull
{
    protected Entity(TId id) => Id = id;

    /// <summary>Constructor for materialization by the persistence layer.</summary>
    protected Entity() => Id = default!;

    public TId Id { get; protected init; }
}

/// <summary>Entity with a UUIDv7 key generated on creation.</summary>
public abstract class Entity : Entity<Guid>
{
    protected Entity()
        : base(IdGenerator.NewId())
    {
    }

    protected Entity(Guid id)
        : base(id)
    {
    }
}
