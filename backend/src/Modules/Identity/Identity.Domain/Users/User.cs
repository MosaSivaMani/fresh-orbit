using FreshOrbit.Identity.Public.Events;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Identity.Domain.Users;

/// <summary>Stored as CUSTOMER / STAFF.</summary>
public enum UserType
{
    Customer,
    Staff,
}

/// <summary>Stored as ACTIVE / LOCKED / DISABLED.</summary>
public enum UserStatus
{
    Active,
    Locked,
    Disabled,
}

/// <summary>A login account (customer phone or staff email). States: docs/product/identity.md §2.</summary>
public sealed class User : AggregateRoot
{
    private User()
    {
    }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string? PasswordHash { get; private set; }

    public UserType UserType { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset? PhoneVerifiedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>Consecutive wrong staff passwords since the last sign-in or lock (IDN-R11).</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>D33: end of the current staff lock; the lock is over from this time on (IDN-R12).</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>D33: locks since the last successful sign-in; each one doubles the next lock's length.</summary>
    public int LockoutCount { get; private set; }

    /// <summary>Set when the account was deleted and its personal data removed; it can never be re-activated.</summary>
    public DateTimeOffset? AnonymisedAt { get; private set; }

    /// <summary>IDN-R6: a customer account created by the first OTP verification.</summary>
    public static User RegisterCustomer(string phone, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        return new User { Phone = phone, UserType = UserType.Customer, Status = UserStatus.Active, PhoneVerifiedAt = now };
    }

    public static User CreateStaff(string email, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        return new User { Email = email.Trim(), PasswordHash = passwordHash, UserType = UserType.Staff, Status = UserStatus.Active };
    }

    /// <summary>IDN-R6, IDN-R12: refuses LOCKED and DISABLED accounts.</summary>
    public void EnsureCanSignIn()
    {
        switch (Status)
        {
            case UserStatus.Locked:
                throw new ForbiddenException(IdentityErrors.AccountLocked, "This account is locked for a while after too many wrong passwords.");
            case UserStatus.Disabled:
                throw new ForbiddenException(IdentityErrors.AccountDisabled, "This account is disabled.");
        }
    }

    /// <summary>Successful OTP sign-in of a customer; raises <see cref="CustomerSignedIn"/> for the guest cart merge.</summary>
    public void RecordCustomerSignIn(Guid customerId, bool isNewCustomer, string? guestCartToken, string? deviceLabel, DateTimeOffset now)
    {
        EnsureCanSignIn();
        PhoneVerifiedAt ??= now;
        LastLoginAt = now;
        Raise(new CustomerSignedIn(Id, customerId, isNewCustomer, string.IsNullOrWhiteSpace(guestCartToken) ? null : guestCartToken, deviceLabel, now));
    }

    /// <summary>Successful staff password sign-in: resets the failure counter and the lock doubling (D33).</summary>
    public void RecordPasswordSignIn(DateTimeOffset now)
    {
        LastLoginAt = now;
        FailedLoginCount = 0;
        LockoutCount = 0;
        LockedUntil = null;
    }

    /// <summary>D33: true while a staff lock is running. A LOCKED row without an end time counts as expired.</summary>
    public bool IsLockedAt(DateTimeOffset now) => Status == UserStatus.Locked && LockedUntil is { } until && now < until;

    /// <summary>D33: a lock whose time is over turns the account ACTIVE again; <see cref="LockoutCount"/> is kept.</summary>
    public void ReleaseExpiredLock(DateTimeOffset now)
    {
        if (Status == UserStatus.Locked && !IsLockedAt(now))
        {
            Status = UserStatus.Active;
            LockedUntil = null;
        }
    }

    /// <summary>
    /// IDN-R11/R12, D33: a wrong password on an ACTIVE account. The failure that reaches
    /// <see cref="LockoutPolicy.MaxFailures"/> locks the account for <see cref="LockoutPolicy.LengthOf"/> the new
    /// lock count, starts a fresh failure count and raises <see cref="StaffAccountLocked"/>. Failures on a LOCKED or
    /// DISABLED account are not counted (a running lock is never extended by more guesses).
    /// </summary>
    /// <returns>True when this failure locked the account.</returns>
    public bool RecordFailedPassword(LockoutPolicy policy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (Status != UserStatus.Active)
        {
            return false;
        }

        FailedLoginCount++;
        if (FailedLoginCount < policy.MaxFailures)
        {
            return false;
        }

        LockoutCount++;
        FailedLoginCount = 0;
        Status = UserStatus.Locked;
        LockedUntil = now.Add(policy.LengthOf(LockoutCount));
        Raise(new StaffAccountLocked(Id, LockedUntil.Value, LockoutCount, now));
        return true;
    }

    public void ChangePassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
        FailedLoginCount = 0;
    }

    /// <summary>IDN-R14: a temporary password set by an administrator; unlocks a LOCKED account and resets the doubling.</summary>
    public void ResetPassword(string passwordHash)
    {
        ChangePassword(passwordHash);
        LockoutCount = 0;
        LockedUntil = null;
        if (Status == UserStatus.Locked)
        {
            Status = UserStatus.Active;
        }
    }

    public void Disable() => Status = UserStatus.Disabled;

    public void Enable()
    {
        if (AnonymisedAt is not null)
        {
            throw new ConflictException(IdentityErrors.InvalidStatusTransition, "A deleted account cannot be re-activated.");
        }

        Status = UserStatus.Active;
        FailedLoginCount = 0;
        LockoutCount = 0;
        LockedUntil = null;
    }

    /// <summary>Account deletion: personal data removed, account disabled for good.</summary>
    public void Anonymise(DateTimeOffset now)
    {
        Phone = null;
        Email = null;
        PasswordHash = null;
        Status = UserStatus.Disabled;
        AnonymisedAt ??= now;
    }

    public void ChangeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Email = email.Trim();
    }
}
