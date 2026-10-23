using FreshOrbit.Identity.Domain.Events;
using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Identity.Domain.Tokens;

/// <summary>Why a refresh token stopped working. Stored as ROTATED / LOGOUT / FAMILY_REUSE / SESSIONS_REVOKED.</summary>
public enum RevokeReason
{
    Rotated,
    Logout,
    FamilyReuse,
    SessionsRevoked,
}

/// <summary>An opaque rotating refresh token (IDN-R8, R9). Only the SHA-256 of the token is stored.</summary>
public sealed class RefreshToken : AggregateRoot
{
    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    /// <summary>All tokens descending from one sign-in share the family id.</summary>
    public Guid FamilyId { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public RevokeReason? RevokeReason { get; private set; }

    public Guid? ReplacedById { get; private set; }

    public string? DeviceLabel { get; private set; }

    /// <summary>A token that was replaced by a newer one; presenting it again means it was stolen or replayed.</summary>
    public bool WasRotated => RevokeReason == Tokens.RevokeReason.Rotated && ReplacedById is not null;

    public static RefreshToken Issue(Guid userId, Guid? familyId, string tokenHash, DateTimeOffset now, TimeSpan lifetime, string? deviceLabel)
    {
        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            IssuedAt = now,
            ExpiresAt = now.Add(lifetime),
            DeviceLabel = deviceLabel is { Length: > 100 } ? deviceLabel[..100] : deviceLabel,
        };
        token.FamilyId = familyId ?? token.Id;
        return token;
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    /// <summary>Replaces this token by a new one of the same family.</summary>
    public RefreshToken Rotate(string newTokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        var next = Issue(UserId, FamilyId, newTokenHash, now, lifetime, DeviceLabel);
        RevokedAt = now;
        RevokeReason = Tokens.RevokeReason.Rotated;
        ReplacedById = next.Id;
        return next;
    }

    /// <summary>IDN-R9: this rotated token was presented again (the family is revoked by the caller).</summary>
    public void ReportReuse(DateTimeOffset now) => Raise(new RefreshTokenReuseDetected(UserId, FamilyId, now));

    public void Revoke(DateTimeOffset now, RevokeReason reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokeReason = reason;
    }
}
