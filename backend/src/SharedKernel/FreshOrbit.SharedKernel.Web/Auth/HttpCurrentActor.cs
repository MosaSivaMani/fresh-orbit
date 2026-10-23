using System.Security.Claims;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using Microsoft.AspNetCore.Http;

namespace FreshOrbit.SharedKernel.Web.Auth;

/// <summary><see cref="ICurrentActor"/> read from the validated JWT of the current request.</summary>
public sealed class HttpCurrentActor : ICurrentActor
{
    private readonly Lazy<Snapshot> _snapshot;

    public HttpCurrentActor(IHttpContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        _snapshot = new Lazy<Snapshot>(() => Read(accessor.HttpContext?.User));
    }

    public ActorType ActorType => _snapshot.Value.ActorType;

    public Guid? UserId => _snapshot.Value.UserId;

    public Guid? StaffMemberId => _snapshot.Value.StaffMemberId;

    public Guid? CustomerId => _snapshot.Value.CustomerId;

    public IReadOnlySet<string> Permissions => _snapshot.Value.Permissions;

    public IReadOnlySet<Guid> LocationIds => _snapshot.Value.LocationIds;

    public bool IsAllLocations => _snapshot.Value.IsAllLocations;

    private static Snapshot Read(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(user.FindFirstValue(FreshOrbitClaimTypes.Subject), out var userId)
            || !EnumCodes.TryToEnum<ActorType>(user.FindFirstValue(FreshOrbitClaimTypes.ActorType), out var actorType)
            || actorType is not (ActorType.Customer or ActorType.Staff))
        {
            return Snapshot.Anonymous;
        }

        return new Snapshot(
            actorType,
            userId,
            ParseGuid(user.FindFirstValue(FreshOrbitClaimTypes.StaffMemberId)),
            ParseGuid(user.FindFirstValue(FreshOrbitClaimTypes.CustomerId)),
            user.FindAll(FreshOrbitClaimTypes.Permission).Select(c => c.Value).ToHashSet(StringComparer.Ordinal),
            user.FindAll(FreshOrbitClaimTypes.Location).Select(c => ParseGuid(c.Value)).OfType<Guid>().ToHashSet(),
            string.Equals(user.FindFirstValue(FreshOrbitClaimTypes.AllLocations), "true", StringComparison.OrdinalIgnoreCase));
    }

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;

    private sealed record Snapshot(
        ActorType ActorType,
        Guid? UserId,
        Guid? StaffMemberId,
        Guid? CustomerId,
        IReadOnlySet<string> Permissions,
        IReadOnlySet<Guid> LocationIds,
        bool IsAllLocations)
    {
        public static readonly Snapshot Anonymous = new(ActorType.Anonymous, null, null, null, new HashSet<string>(), new HashSet<Guid>(), false);
    }
}
