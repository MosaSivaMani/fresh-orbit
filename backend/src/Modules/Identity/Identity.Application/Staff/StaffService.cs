using FreshOrbit.Identity.Application.Auth;
using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.Identity.Domain.Tokens;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Identity.Application.Staff;

public interface IStaffService
{
    Task<StaffResponse> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken);

    Task<StaffResponse> UpdateAsync(Guid staffMemberId, uint expectedVersion, UpdateStaffRequest request, CancellationToken cancellationToken);

    Task<StaffResponse> ResetPasswordAsync(Guid staffMemberId, uint expectedVersion, ResetStaffPasswordRequest request, CancellationToken cancellationToken);
}

/// <summary>Staff administration (IDN-R13–R16).</summary>
public sealed class StaffService(
    IUserRepository users,
    IStaffRepository staff,
    IRoleRepository roles,
    IPasswordHashing passwords,
    SessionRevoker revoker,
    IStaffReads reads,
    IUnitOfWork unitOfWork) : IStaffService
{
    public async Task<StaffResponse> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var email = EmailAddresses.Normalize(request.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException(IdentityErrors.Duplicate, "A user with this email already exists.", "email");
        }

        PasswordPolicy.Ensure(request.TemporaryPassword, "temporaryPassword");
        await EnsureRolesExistAsync(request.RoleIds, cancellationToken);
        var user = User.CreateStaff(email, passwords.Hash(request.TemporaryPassword));
        var member = StaffMember.Create(user.Id, email, new StaffData(request.FullName, [.. request.RoleIds], request.AllLocations, [.. request.LocationIds ?? []], true));
        users.Add(user);
        staff.Add(member);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await reads.GetAsync(member.Id, cancellationToken))!;
    }

    public async Task<StaffResponse> UpdateAsync(Guid staffMemberId, uint expectedVersion, UpdateStaffRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var member = await staff.GetAsync(staffMemberId, cancellationToken) ?? throw NotFound();
        unitOfWork.SetExpectedVersion(member, expectedVersion);
        await EnsureRolesExistAsync(request.RoleIds, cancellationToken);
        var admin = await roles.FindByCodeAsync(IdentityErrors.AdminRoleCode, cancellationToken);
        var wasActiveAdmin = admin is not null && member.IsActive && member.HasRole(admin.Id);

        member.Update(new StaffData(request.FullName, [.. request.RoleIds], request.AllLocations, [.. request.LocationIds ?? []], request.IsActive));

        var isActiveAdmin = admin is not null && member.IsActive && member.HasRole(admin.Id);
        if (wasActiveAdmin && !isActiveAdmin && await staff.CountActiveWithRoleAsync(admin!.Id, member.Id, cancellationToken) == 0)
        {
            throw new DomainException(IdentityErrors.LastAdmin, "This is the last active administrator; give another staff member the ADMIN role first.", "roleIds");
        }

        var user = await users.GetAsync(member.UserId, cancellationToken) ?? throw NotFound();
        if (!member.IsActive && user.Status != UserStatus.Disabled)
        {
            user.Disable();
            await revoker.RevokeAllAsync(user.Id, RevokeReason.SessionsRevoked, cancellationToken);
        }
        else if (member.IsActive && user.Status == UserStatus.Disabled)
        {
            user.Enable();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await reads.GetAsync(member.Id, cancellationToken))!;
    }

    public async Task<StaffResponse> ResetPasswordAsync(Guid staffMemberId, uint expectedVersion, ResetStaffPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var member = await staff.GetAsync(staffMemberId, cancellationToken) ?? throw NotFound();
        unitOfWork.SetExpectedVersion(member, expectedVersion);
        PasswordPolicy.Ensure(request.TemporaryPassword, "temporaryPassword");
        var user = await users.GetAsync(member.UserId, cancellationToken) ?? throw NotFound();
        user.ResetPassword(passwords.Hash(request.TemporaryPassword));
        await revoker.RevokeAllAsync(user.Id, RevokeReason.SessionsRevoked, cancellationToken);
        member.RecordPasswordReset();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await reads.GetAsync(member.Id, cancellationToken))!;
    }

    private async Task EnsureRolesExistAsync(IReadOnlyList<Guid>? roleIds, CancellationToken cancellationToken)
    {
        var wanted = (roleIds ?? []).Distinct().ToList();
        var found = await roles.GetManyAsync(wanted, cancellationToken);
        if (found.Count != wanted.Count)
        {
            throw new DomainException(IdentityErrors.RoleNotFound, "The roleIds name a role that does not exist.", "roleIds");
        }
    }

    private static NotFoundException NotFound() => new("No staff member has this id.");
}
