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

/// <summary>Staff e-mail + password login with the D33 time-based lockout (IDN-R11–R14, IDN-R20).</summary>
public sealed class StaffAuthService(
    IUserRepository users,
    IStaffRepository staff,
    IRoleRepository roles,
    IStaffLoginFailureRepository failures,
    IPasswordHashing passwords,
    SessionIssuer sessions,
    SessionRevoker revoker,
    IRequestContext requestContext,
    IUnitOfWork unitOfWork,
    IClock clock,
    IdentitySettings settings) : IStaffAuthService
{
    // Verified against when no real hash is checked, so every refusal takes about the same time.
    private static string? s_dummyHash;

    public async Task<StaffLoginResponse> LoginAsync(StaffLoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.UtcNow;
        var email = EmailAddresses.Normalize(request.Email);
        var ip = requestContext.ClientIp;
        var user = await users.FindStaffByEmailAsync(email, cancellationToken);
        await EnforceFailureLimitAsync(email, ip, user?.LastLoginAt, now, cancellationToken);

        // D33: the status is checked before the password. Unknown, LOCKED, DISABLED and inactive staff all get the
        // same INVALID_CREDENTIALS after one hash verification, so neither the answer nor its timing tells them apart.
        user?.ReleaseExpiredLock(now);
        var member = user is null ? null : await staff.FindByUserAsync(user.Id, cancellationToken);
        if (user?.PasswordHash is null || user.Status != UserStatus.Active || member is not { IsActive: true })
        {
            passwords.Verify(s_dummyHash ??= passwords.Hash(Guid.NewGuid().ToString()), request.Password, out _);
            throw await RefuseAsync(email, ip, now, cancellationToken);
        }

        if (!passwords.Verify(user.PasswordHash, request.Password, out var needsRehash))
        {
            user.RecordFailedPassword(settings.StaffLockoutPolicy(), now);
            throw await RefuseAsync(email, ip, now, cancellationToken);
        }

        if (needsRehash)
        {
            user.ChangePassword(passwords.Hash(request.Password));
        }

        user.RecordPasswordSignIn(now);
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

    /// <summary>
    /// IDN-R20: at most <see cref="IdentitySettings.StaffLoginFailuresPerEmailIp"/> failures per e-mail + client IP in
    /// the window; failures before the account's last successful sign-in no longer count. Checked before any password.
    /// </summary>
    private async Task EnforceFailureLimitAsync(string email, string? ip, DateTimeOffset? lastLoginAt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var window = TimeSpan.FromMinutes(settings.StaffLoginFailureWindowMinutes);
        var since = lastLoginAt is { } last && last > now - window ? last : now - window;
        var times = await failures.GetFailureTimesAsync(StaffLoginFailure.HashEmail(email), StaffLoginFailure.KeyOf(ip), since, cancellationToken);
        var limit = settings.StaffLoginFailuresPerEmailIp;
        if (times.Count >= limit)
        {
            // Allowed again once enough of the counted failures have left the window.
            var wait = times[times.Count - limit] + window - now;
            throw new TooManyRequestsException(IdentityErrors.RateLimited, "Too many failed sign-ins for this email; wait before trying again.", (int)Math.Ceiling(wait.TotalSeconds));
        }
    }

    /// <summary>Records the failure for IDN-R20, saves (with any counter or lock change) and returns the refusal.</summary>
    private async Task<UnauthorizedException> RefuseAsync(string email, string? ip, DateTimeOffset now, CancellationToken cancellationToken)
    {
        failures.Add(StaffLoginFailure.Record(email, ip, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InvalidCredentials();
    }

    private static UnauthorizedException InvalidCredentials() => new(IdentityErrors.InvalidCredentials, "The email or password is wrong.");
}
