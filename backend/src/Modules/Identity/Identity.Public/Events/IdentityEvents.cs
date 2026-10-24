using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Identity.Public.Events;

/// <summary>
/// A customer signed in with an OTP. Cart merges the guest cart identified by <paramref name="GuestCartToken"/>
/// (the raw token the client sent; Cart hashes it to find <c>carts.guest_token_hash</c>).
/// </summary>
public sealed record CustomerSignedIn(Guid UserId, Guid CustomerId, bool IsNewCustomer, string? GuestCartToken, string? DeviceLabel, DateTimeOffset SignedInAt) : DomainEvent;

public sealed record StaffMemberCreated(Guid StaffMemberId, Guid UserId, string Email, string FullName) : DomainEvent;

public sealed record StaffPasswordReset(Guid StaffMemberId, Guid UserId) : DomainEvent;

/// <summary>A rotated refresh token was presented again; its whole family was revoked.</summary>
public sealed record RefreshTokenReuseDetected(Guid UserId, Guid FamilyId, DateTimeOffset DetectedAt) : DomainEvent;

/// <summary>
/// D33: a staff account was locked after too many wrong passwords, until <paramref name="LockedUntil"/>.
/// <paramref name="LockoutCount"/> is the number of locks since the last successful sign-in (1 = first). Notifications
/// e-mails the staff managers (<c>STAFF_ACCOUNT_LOCKED</c>). No e-mail address in the payload; the handler reads it.
/// </summary>
public sealed record StaffAccountLocked(Guid UserId, DateTimeOffset LockedUntil, int LockoutCount, DateTimeOffset LockedAt) : DomainEvent;
