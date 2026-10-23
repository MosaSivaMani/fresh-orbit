using FreshOrbit.Identity.Application.Auth;
using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Domain.Roles;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.Identity.Domain.Tokens;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Identity.Application.Staff;

public interface IStaffAuthService
{
    Task<StaffLoginResponse> LoginAsync(StaffLoginRequest request, CancellationToken cancellationToken);

    Task<StaffProfileResponse> GetProfileAsync(Guid staffMemberId, CancellationToken cancellationToken);

    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}

/// <summary>Staff e-mail + password login with lockout (IDN-R11–R14).</summary>
public sealed class StaffAuthService(
    IUserRepository users,
    IStaffRepository staff,
    IRoleRepository roles,
    IPasswordHashing passwords,
    SessionIssuer sessions,
    SessionRevoker revoker,
    IUnitOfWork unitOfWork,
    IClock clock,
    IdentitySettings settings) : IStaffAuthService
{
    // Verified against for unknown e-mails so both answers take similar time.
    private static string? s_dummyHash;

    public async Task<StaffLoginResponse> LoginAsync(StaffLoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await users.FindStaffByEmailAsync(EmailAddresses.Normalize(request.Email), cancellationToken);
        if (user?.PasswordHash is null)
        {
            passwords.Verify(s_dummyHash ??= passwords.Hash(Guid.NewGuid().ToString()), request.Password, out _);
            throw InvalidCredentials();
        }

        if (!passwords.Verify(user.PasswordHash, request.Password, out var needsRehash))
        {
            var wasLocked = user.Status == UserStatus.Locked;
            user.RecordFailedPassword(settings.StaffMaxFailedLogins);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            if (!wasLocked && user.Status == UserStatus.Locked)
            {
                throw new ForbiddenException(IdentityErrors.AccountLocked, "Too many wrong passwords; this account is now locked.");
            }

            throw InvalidCredentials();
        }

        user.EnsureCanSignIn();
        var member = await staff.FindByUserAsync(user.Id, cancellationToken);
        if (member is null || !member.IsActive)
        {
            throw new ForbiddenException(IdentityErrors.AccountDisabled, "This staff account is inactive.");
        }

        if (needsRehash)
        {
            user.ChangePassword(passwords.Hash(request.Password));
        }

        user.RecordPasswordSignIn(clock.UtcNow);
        var session = await sessions.IssueStaffAsync(user, member, null, request.DeviceLabel, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return StaffLoginResponse.From(session, await ProfileAsync(member, user, cancellationToken));
    }

    public async Task<StaffProfileResponse> GetProfileAsync(Guid staffMemberId, CancellationToken cancellationToken)
    {
        var member = await staff.GetAsync(staffMemberId, cancellationToken) ?? throw new NotFoundException("No staff member has this id.");
        var user = await users.GetAsync(member.UserId, cancellationToken) ?? throw new NotFoundException("No staff member has this id.");
        return await ProfileAsync(member, user, cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await users.GetAsync(userId, cancellationToken);
        if (user?.PasswordHash is null || !passwords.Verify(user.PasswordHash, request.CurrentPassword, out _))
        {
            throw InvalidCredentials();
        }

        PasswordPolicy.Ensure(request.NewPassword, "newPassword");
        user.ChangePassword(passwords.Hash(request.NewPassword));
        await revoker.RevokeAllAsync(user.Id, RevokeReason.SessionsRevoked, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<StaffProfileResponse> ProfileAsync(StaffMember member, User user, CancellationToken cancellationToken)
    {
        var memberRoles = await roles.GetManyAsync([.. member.RoleIds], cancellationToken);
        var permissions = await sessions.PermissionsOfAsync(member, cancellationToken);
        return new StaffProfileResponse(
            member.Id,
            user.Id,
            user.Email,
            member.FullName,
            [.. memberRoles.OrderBy(r => r.Code, StringComparer.Ordinal).Select(ToRef)],
            permissions,
            member.AllLocations,
            [.. member.LocationIds]);
    }

    internal static StaffRoleRef ToRef(Role role) => new(role.Id, role.Code, role.Name);

    private static UnauthorizedException InvalidCredentials() => new(IdentityErrors.InvalidCredentials, "The email or password is wrong.");
}
