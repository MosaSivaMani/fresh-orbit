using System.Globalization;
using FluentValidation;
using FreshOrbit.Identity.Domain.Roles;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Identity.Application.Roles;

public sealed record CreateRoleRequest(string Code, string Name, IReadOnlyList<string> Permissions);

public sealed record UpdateRoleRequest(string Name, IReadOnlyList<string> Permissions);

public sealed record RoleResponse(Guid Id, string Code, string Name, bool IsSystem, IReadOnlyList<string> Permissions, string Version);

public sealed record RoleListResponse(IReadOnlyList<RoleResponse> Items);

public sealed record PermissionResponse(string Code, string Description);

public sealed record PermissionListResponse(IReadOnlyList<PermissionResponse> Items);

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40).Must(ErrorCodes.IsUpperSnake).WithErrorCode("CODE_INVALID")
            .WithMessage("The code must be UPPER_SNAKE, e.g. NIGHT_PACKER.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Permissions).NotNull();
    }
}

public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Permissions).NotNull();
    }
}

public interface IRoleService
{
    Task<RoleListResponse> ListAsync(CancellationToken cancellationToken);

    Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken);

    Task<RoleResponse> UpdateAsync(Guid roleId, uint expectedVersion, UpdateRoleRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid roleId, uint expectedVersion, CancellationToken cancellationToken);

    Task<PermissionListResponse> ListPermissionsAsync(CancellationToken cancellationToken);
}

/// <summary>Roles and the permission catalogue (IDN-R17).</summary>
public sealed class RoleService(IRoleRepository roles, IStaffRepository staff, IUnitOfWork unitOfWork) : IRoleService
{
    public async Task<RoleListResponse> ListAsync(CancellationToken cancellationToken)
        => new([.. (await roles.ListAsync(cancellationToken)).OrderBy(r => !r.IsSystem).ThenBy(r => r.Code, StringComparer.Ordinal).Select(ToResponse)]);

    public async Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await roles.FindByCodeAsync(request.Code, cancellationToken) is not null)
        {
            throw new ConflictException(IdentityErrors.Duplicate, "A role with this code already exists.", "code");
        }

        await EnsurePermissionsExistAsync(request.Permissions, cancellationToken);
        var role = Role.Create(request.Code, request.Name, false, request.Permissions);
        roles.Add(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(role);
    }

    public async Task<RoleResponse> UpdateAsync(Guid roleId, uint expectedVersion, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var role = await roles.GetAsync(roleId, cancellationToken) ?? throw NotFound();
        unitOfWork.SetExpectedVersion(role, expectedVersion);
        await EnsurePermissionsExistAsync(request.Permissions, cancellationToken);
        role.Update(request.Name, request.Permissions);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(role);
    }

    public async Task DeleteAsync(Guid roleId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var role = await roles.GetAsync(roleId, cancellationToken) ?? throw NotFound();
        unitOfWork.SetExpectedVersion(role, expectedVersion);
        role.EnsureDeletable();
        if (await staff.AnyWithRoleAsync(role.Id, cancellationToken))
        {
            throw new ConflictException(IdentityErrors.RoleInUse, "This role is still assigned to staff; remove it from them first.");
        }

        roles.Remove(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PermissionListResponse> ListPermissionsAsync(CancellationToken cancellationToken)
        => new([.. (await roles.ListPermissionsAsync(cancellationToken)).OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => new PermissionResponse(p.Id, p.Description))]);

    public static RoleResponse ToResponse(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return new RoleResponse(role.Id, role.Code, role.Name, role.IsSystem, [.. role.PermissionCodes.Order(StringComparer.Ordinal)], role.Version.ToString(CultureInfo.InvariantCulture));
    }

    private async Task EnsurePermissionsExistAsync(IReadOnlyList<string>? codes, CancellationToken cancellationToken)
    {
        var known = (await roles.ListPermissionsAsync(cancellationToken)).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var unknown = (codes ?? []).Where(c => !known.Contains(c)).ToList();
        if (unknown.Count > 0)
        {
            throw new DomainException(IdentityErrors.PermissionUnknown, $"The permissions include unknown codes: {string.Join(", ", unknown)}.", "permissions");
        }
    }

    private static NotFoundException NotFound() => new("No role has this id.");
}
