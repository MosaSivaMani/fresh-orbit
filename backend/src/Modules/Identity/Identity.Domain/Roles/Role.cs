using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Identity.Domain.Roles;

/// <summary>A named set of permissions (IDN-R17). System roles come from reference data.</summary>
public sealed class Role : AggregateRoot
{
    private readonly List<RolePermission> _permissions = [];

    private Role()
    {
        Code = Name = string.Empty;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public bool IsSystem { get; private set; }

    /// <summary>Incremented on every change so a permission change alone still bumps the row version.</summary>
    public int Revision { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    public bool IsAdmin => Code == IdentityErrors.AdminRoleCode;

    public IEnumerable<string> PermissionCodes => _permissions.Select(p => p.PermissionCode);

    public static Role Create(string code, string name, bool isSystem, IEnumerable<string> permissionCodes)
    {
        ErrorCodes.EnsureValid(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var role = new Role { Code = code, Name = name.Trim(), IsSystem = isSystem };
        role.SetPermissions(permissionCodes);
        return role;
    }

    /// <summary>Renames (custom roles only) and replaces the permission set (not for ADMIN).</summary>
    public void Update(string name, IEnumerable<string> permissionCodes)
    {
        ArgumentNullException.ThrowIfNull(permissionCodes);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var codes = permissionCodes.Distinct(StringComparer.Ordinal).ToList();
        if (IsSystem && !string.Equals(name.Trim(), Name, StringComparison.Ordinal))
        {
            throw new DomainException(IdentityErrors.SystemRoleReadOnly, "A system role cannot be renamed.", "name");
        }

        if (IsAdmin && !codes.ToHashSet(StringComparer.Ordinal).SetEquals(PermissionCodes))
        {
            throw new DomainException(IdentityErrors.SystemRoleReadOnly, "The ADMIN role always holds every permission; its permissions cannot be changed.", "permissions");
        }

        Name = name.Trim();
        Revision++;
        SetPermissions(codes);
    }

    public void EnsureDeletable()
    {
        if (IsSystem)
        {
            throw new DomainException(IdentityErrors.SystemRoleReadOnly, "A system role cannot be deleted.");
        }
    }

    /// <summary>Reference data: adds permissions the role is missing (never removes).</summary>
    public void GrantMissing(IEnumerable<string> permissionCodes)
    {
        ArgumentNullException.ThrowIfNull(permissionCodes);
        var missing = permissionCodes.Where(c => _permissions.All(p => p.PermissionCode != c)).ToList();
        if (missing.Count > 0)
        {
            Revision++;
        }

        foreach (var code in missing)
        {
            _permissions.Add(new RolePermission(Id, code));
        }
    }

    private void SetPermissions(IEnumerable<string> permissionCodes)
    {
        var wanted = permissionCodes.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        _permissions.RemoveAll(p => !wanted.Contains(p.PermissionCode));
        GrantMissing(wanted);
    }
}

/// <summary>Row of <c>role_permissions</c>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Domain term: a permission code row.")]
public sealed class RolePermission
{
    public RolePermission(Guid roleId, string permissionCode)
    {
        RoleId = roleId;
        PermissionCode = permissionCode;
    }

    public Guid RoleId { get; private set; }

    public string PermissionCode { get; private set; }
}

/// <summary>A permission code (dotted lowercase, e.g. <c>orders.pack</c>); reference data.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Domain term: a permission code row.")]
public sealed class Permission : Entity<string>
{
    public Permission(string id, string description)
        : base(id)
    {
        Description = description;
    }

    public string Description { get; private set; }

    public void Describe(string description) => Description = description;
}
