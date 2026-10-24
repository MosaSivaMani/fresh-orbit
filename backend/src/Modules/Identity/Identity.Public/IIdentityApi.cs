namespace FreshOrbit.Identity.Public;

/// <summary>What other modules may ask the Identity module.</summary>
public interface IIdentityApi
{
    /// <summary>The user, or null when it does not exist.</summary>
    Task<UserSummary?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The requested users that exist (missing ids are simply absent).</summary>
    Task<IReadOnlyList<UserSummary>> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>The user with this E.164 phone, or null.</summary>
    Task<Guid?> FindUserIdByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    /// <summary>User ids by E.164 phone for the phones that have a user (one query; missing phones are absent).</summary>
    Task<IReadOnlyDictionary<string, Guid>> FindUserIdsByPhonesAsync(IReadOnlyCollection<string> phones, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not implemented by this test double.");

    /// <summary>The requested staff members that exist (missing ids are simply absent).</summary>
    Task<IReadOnlyList<StaffSummary>> GetStaffMembersAsync(IReadOnlyCollection<Guid> staffMemberIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// User ids of active staff holding <paramref name="permission"/>; with <paramref name="fulfillmentLocationId"/>
    /// only those allowed at that location (all-locations staff included).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetStaffUserIdsWithPermissionAsync(string permission, Guid? fulfillmentLocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Blocks (DISABLED, refresh tokens revoked) or unblocks (ACTIVE) a customer user. Tracked on the shared
    /// DbContext and saved by the caller; never saves.
    /// </summary>
    Task SetUserBlockedAsync(Guid userId, bool blocked, CancellationToken cancellationToken = default);

    /// <summary>
    /// Account deletion: clears phone and email, sets DISABLED and revokes refresh tokens. Tracked; saved by the caller.
    /// </summary>
    Task AnonymiseUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Kind of login account. Stored as CUSTOMER / STAFF.</summary>
public enum UserType
{
    Customer,
    Staff,
}

/// <summary>Login account status. Stored as ACTIVE / LOCKED / DISABLED.</summary>
public enum UserStatus
{
    Active,
    Locked,
    Disabled,
}

/// <param name="Phone">E.164 phone number.</param>
public sealed record UserSummary(Guid UserId, string? Phone, string? Email, UserType UserType)
{
    public UserStatus Status { get; init; } = UserStatus.Active;
}

public sealed record StaffSummary(Guid StaffMemberId, Guid UserId, string FullName, string? Email, bool IsActive);
