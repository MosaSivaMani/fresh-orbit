using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Identity.Domain.Events;

/// <summary>
/// A customer signed in with an OTP. Cart merges the guest cart identified by <paramref name="GuestCartToken"/>
/// (the raw token the client sent; Cart hashes it to find <c>carts.guest_token_hash</c>).
/// </summary>
public sealed record CustomerSignedIn(Guid UserId, Guid CustomerId, bool IsNewCustomer, string? GuestCartToken, string? DeviceLabel, DateTimeOffset SignedInAt) : DomainEvent;

public sealed record StaffMemberCreated(Guid StaffMemberId, Guid UserId, string Email, string FullName) : DomainEvent;

public sealed record StaffPasswordReset(Guid StaffMemberId, Guid UserId) : DomainEvent;

/// <summary>A rotated refresh token was presented again; its whole family was revoked.</summary>
public sealed record RefreshTokenReuseDetected(Guid UserId, Guid FamilyId, DateTimeOffset DetectedAt) : DomainEvent;
