using FreshOrbit.Identity.Domain.Events;
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

    public int FailedLoginCount { get; private set; }

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
                throw new DomainException(IdentityErrors.AccountLocked, "This account is locked; ask an administrator to reset the password.");
            case UserStatus.Disabled:
                throw new DomainException(IdentityErrors.AccountDisabled, "This account is disabled.");
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

    /// <summary>Successful staff password sign-in: resets the failure counter.</summary>
    public void RecordPasswordSignIn(DateTimeOffset now)
    {
        LastLoginAt = now;
        FailedLoginCount = 0;
    }

    /// <summary>IDN-R11/R12: a wrong password; locks the account at <paramref name="maxFailures"/>.</summary>
    public void RecordFailedPassword(int maxFailures)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= maxFailures && Status == UserStatus.Active)
        {
            Status = UserStatus.Locked;
        }
    }

    public void ChangePassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
        FailedLoginCount = 0;
    }

    /// <summary>IDN-R14: a temporary password set by an administrator; unlocks a LOCKED account.</summary>
    public void ResetPassword(string passwordHash)
    {
        ChangePassword(passwordHash);
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
