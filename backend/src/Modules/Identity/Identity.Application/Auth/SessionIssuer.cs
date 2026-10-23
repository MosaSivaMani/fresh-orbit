using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.Identity.Domain.Tokens;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Identity.Application.Auth;

public sealed record SessionTokensResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

/// <summary>Issues an access token plus a new (or rotated) refresh token; the caller saves (IDN-R7, R8).</summary>
public sealed class SessionIssuer(IRefreshTokenRepository tokens, IRoleRepository roles, IAccessTokenIssuer accessTokens, IClock clock, IdentitySettings settings)
{
    public SessionTokensResponse IssueCustomer(User user, Guid customerId, RefreshToken? rotateFrom, string? deviceLabel)
    {
        ArgumentNullException.ThrowIfNull(user);
        var access = accessTokens.Issue(new AccessTokenClaims(user.Id, false, customerId, null, [], [], false));
        return WithRefreshToken(user, access, rotateFrom, deviceLabel, TimeSpan.FromDays(settings.CustomerRefreshTokenDays));
    }

    public async Task<SessionTokensResponse> IssueStaffAsync(User user, StaffMember staff, RefreshToken? rotateFrom, string? deviceLabel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(staff);
        var permissions = await PermissionsOfAsync(staff, cancellationToken);
        var access = accessTokens.Issue(new AccessTokenClaims(user.Id, true, null, staff.Id, permissions, [.. staff.LocationIds], staff.AllLocations));
        return WithRefreshToken(user, access, rotateFrom, deviceLabel, TimeSpan.FromHours(settings.StaffRefreshTokenHours));
    }

    /// <summary>Union of the staff member's role permissions (ADMIN holds every permission).</summary>
    public async Task<IReadOnlyList<string>> PermissionsOfAsync(StaffMember staff, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(staff);
        var staffRoles = await roles.GetManyAsync([.. staff.RoleIds], cancellationToken);
        if (staffRoles.Any(r => r.IsAdmin))
        {
            return [.. (await roles.ListPermissionsAsync(cancellationToken)).Select(p => p.Id).Order(StringComparer.Ordinal)];
        }

        return [.. staffRoles.SelectMany(r => r.PermissionCodes).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private SessionTokensResponse WithRefreshToken(User user, IssuedAccessToken access, RefreshToken? rotateFrom, string? deviceLabel, TimeSpan lifetime)
    {
        var raw = OpaqueTokens.NewRefreshToken();
        var hash = OpaqueTokens.Hash(raw);
        var now = clock.UtcNow;
        var refresh = rotateFrom is null
            ? RefreshToken.Issue(user.Id, null, hash, now, lifetime, deviceLabel)
            : rotateFrom.Rotate(hash, now, lifetime);
        tokens.Add(refresh);
        return new SessionTokensResponse(access.Token, access.ExpiresAt, raw, refresh.ExpiresAt);
    }
}

/// <summary>Revokes every active refresh token of a user (password change, block, deletion).</summary>
public sealed class SessionRevoker(IRefreshTokenRepository tokens, IClock clock)
{
    public async Task RevokeAllAsync(Guid userId, RevokeReason reason, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        foreach (var token in await tokens.GetActiveForUserAsync(userId, now, cancellationToken))
        {
            token.Revoke(now, reason);
        }
    }
}
