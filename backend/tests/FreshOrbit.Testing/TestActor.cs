using FreshOrbit.SharedKernel.Actors;

namespace FreshOrbit.Testing;

/// <summary>An <see cref="ICurrentActor"/> with whatever identity a test needs.</summary>
public sealed record TestActor : ICurrentActor
{
    public ActorType ActorType { get; init; } = ActorType.System;

    public Guid? UserId { get; init; }

    public Guid? StaffMemberId { get; init; }

    public Guid? CustomerId { get; init; }

    public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>();

    public IReadOnlySet<Guid> LocationIds { get; init; } = new HashSet<Guid>();

    public bool IsAllLocations { get; init; }

    public static TestActor System() => new() { ActorType = ActorType.System, IsAllLocations = true };

    public static TestActor Customer(Guid? userId = null, Guid? customerId = null)
        => new() { ActorType = ActorType.Customer, UserId = userId ?? Guid.CreateVersion7(), CustomerId = customerId ?? Guid.CreateVersion7() };

    public static TestActor Staff(IEnumerable<string>? permissions = null, IEnumerable<Guid>? locationIds = null, bool allLocations = false, Guid? userId = null)
        => new()
        {
            ActorType = ActorType.Staff,
            UserId = userId ?? Guid.CreateVersion7(),
            StaffMemberId = Guid.CreateVersion7(),
            Permissions = (permissions ?? []).ToHashSet(StringComparer.Ordinal),
            LocationIds = (locationIds ?? []).ToHashSet(),
            IsAllLocations = allLocations,
        };
}
