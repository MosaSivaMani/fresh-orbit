namespace FreshOrbit.SharedKernel.Actors;

/// <summary>Actor for the Worker, migrations and other background processes.</summary>
public sealed class SystemActor : ICurrentActor
{
    public static readonly SystemActor Instance = new();

    public ActorType ActorType => ActorType.System;

    public Guid? UserId => null;

    public Guid? StaffMemberId => null;

    public Guid? CustomerId => null;

    public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();

    public IReadOnlySet<Guid> LocationIds { get; } = new HashSet<Guid>();

    public bool IsAllLocations => true;
}
