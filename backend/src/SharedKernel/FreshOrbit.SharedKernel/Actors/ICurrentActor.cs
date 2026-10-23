namespace FreshOrbit.SharedKernel.Actors;

/// <summary>The person or process the current request or job runs for.</summary>
public interface ICurrentActor
{
    ActorType ActorType { get; }

    /// <summary>Identity user id (<c>sub</c>); null for anonymous and system actors.</summary>
    Guid? UserId { get; }

    Guid? StaffMemberId { get; }

    Guid? CustomerId { get; }

    IReadOnlySet<string> Permissions { get; }

    /// <summary>Fulfillment locations a staff member may act on (ignored when <see cref="IsAllLocations"/>).</summary>
    IReadOnlySet<Guid> LocationIds { get; }

    bool IsAllLocations { get; }

    bool IsAuthenticated => ActorType is ActorType.Customer or ActorType.Staff;

    bool HasPermission(string permission) => Permissions.Contains(permission);

    bool CanAccessLocation(Guid fulfillmentLocationId) => ActorType == ActorType.System || IsAllLocations || LocationIds.Contains(fulfillmentLocationId);
}
