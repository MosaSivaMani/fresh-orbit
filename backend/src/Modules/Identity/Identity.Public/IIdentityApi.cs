namespace FreshOrbit.Identity.Public;

/// <summary>What other modules may ask the Identity module.</summary>
public interface IIdentityApi
{
    /// <summary>The user, or null when it does not exist.</summary>
    Task<UserSummary?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Kind of login account. Stored as CUSTOMER / STAFF.</summary>
public enum UserType
{
    Customer,
    Staff,
}

/// <param name="Phone">E.164 phone number.</param>
public sealed record UserSummary(Guid UserId, string? Phone, string? Email, UserType UserType);
