using FreshOrbit.Identity.Public.Events;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Identity.Domain.Staff;

/// <summary>A staff member: roles and location access (IDN-R15, R16).</summary>
public sealed class StaffMember : AggregateRoot
{
    private readonly List<StaffRole> _roles = [];
    private readonly List<StaffLocationAccess> _locations = [];

    private StaffMember()
    {
        FullName = string.Empty;
    }

    public Guid UserId { get; private set; }

    public string FullName { get; private set; }

    public bool AllLocations { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Incremented on every change so a change of roles or locations alone still bumps the row version.</summary>
    public int Revision { get; private set; }

    public IReadOnlyCollection<StaffRole> Roles => _roles.AsReadOnly();

    public IReadOnlyCollection<StaffLocationAccess> Locations => _locations.AsReadOnly();

    public IEnumerable<Guid> RoleIds => _roles.Select(r => r.RoleId);

    public IEnumerable<Guid> LocationIds => _locations.Select(l => l.FulfillmentLocationId);

    public static StaffMember Create(Guid userId, string email, StaffData data)
    {
        var staff = new StaffMember { UserId = userId, IsActive = true };
        staff.Apply(data with { IsActive = true });
        staff.Raise(new StaffMemberCreated(staff.Id, userId, email, staff.FullName));
        return staff;
    }

    public void Update(StaffData data) => Apply(data);

    public void RecordPasswordReset()
    {
        Revision++;
        Raise(new StaffPasswordReset(Id, UserId));
    }

    public bool HasRole(Guid roleId) => _roles.Any(r => r.RoleId == roleId);

    private void Apply(StaffData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(data.FullName) || data.FullName.Trim().Length > 100)
        {
            errors.Add(new FieldError("fullName", "FULL_NAME_INVALID", "The fullName must be 1 to 100 characters."));
        }

        if (data.RoleIds.Count == 0)
        {
            errors.Add(new FieldError("roleIds", "ROLE_REQUIRED", "The roleIds must name at least one role."));
        }

        if (!data.AllLocations && data.LocationIds.Count == 0)
        {
            errors.Add(new FieldError("locationIds", "LOCATION_REQUIRED", "The locationIds must name at least one location when allLocations is false."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        Revision++;
        FullName = data.FullName.Trim();
        AllLocations = data.AllLocations;
        IsActive = data.IsActive;

        var roleIds = data.RoleIds.ToHashSet();
        _roles.RemoveAll(r => !roleIds.Contains(r.RoleId));
        foreach (var roleId in roleIds.Where(id => _roles.All(r => r.RoleId != id)))
        {
            _roles.Add(new StaffRole(Id, roleId));
        }

        var locationIds = data.AllLocations ? [] : data.LocationIds.ToHashSet();
        _locations.RemoveAll(l => !locationIds.Contains(l.FulfillmentLocationId));
        foreach (var locationId in locationIds.Where(id => _locations.All(l => l.FulfillmentLocationId != id)))
        {
            _locations.Add(new StaffLocationAccess(Id, locationId));
        }
    }
}

public sealed record StaffData(string FullName, IReadOnlyCollection<Guid> RoleIds, bool AllLocations, IReadOnlyCollection<Guid> LocationIds, bool IsActive);

/// <summary>Row of <c>staff_roles</c>.</summary>
public sealed class StaffRole
{
    public StaffRole(Guid staffMemberId, Guid roleId)
    {
        StaffMemberId = staffMemberId;
        RoleId = roleId;
    }

    public Guid StaffMemberId { get; private set; }

    public Guid RoleId { get; private set; }
}

/// <summary>Row of <c>staff_location_access</c>.</summary>
public sealed class StaffLocationAccess
{
    public StaffLocationAccess(Guid staffMemberId, Guid fulfillmentLocationId)
    {
        StaffMemberId = staffMemberId;
        FulfillmentLocationId = fulfillmentLocationId;
    }

    public Guid StaffMemberId { get; private set; }

    public Guid FulfillmentLocationId { get; private set; }
}
