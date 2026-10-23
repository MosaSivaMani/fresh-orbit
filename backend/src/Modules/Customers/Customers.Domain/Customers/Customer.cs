using FreshOrbit.Customers.Public.Events;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Customers.Domain.Customers;

/// <summary>Stored as ACTIVE / DELETED.</summary>
public enum CustomerStatus
{
    Active,
    Deleted,
}

/// <summary>A customer's profile (docs/product/customers.md).</summary>
public sealed class Customer : AggregateRoot
{
    private Customer()
    {
    }

    public Guid UserId { get; private set; }

    public string? FullName { get; private set; }

    public string? Email { get; private set; }

    public bool MarketingOptIn { get; private set; }

    public CustomerStatus Status { get; private set; }

    public bool IsBlocked { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Stamped on every address change so concurrent address edits of one customer race on this row.</summary>
    public DateTimeOffset? AddressesChangedAt { get; private set; }

    public static Customer Register(Guid userId)
    {
        var customer = new Customer { UserId = userId, Status = CustomerStatus.Active };
        customer.Raise(new CustomerRegistered(customer.Id, userId));
        return customer;
    }

    /// <summary>CUS-R1.</summary>
    public void EnsureActive()
    {
        if (Status == CustomerStatus.Deleted)
        {
            throw new ForbiddenException(CustomersErrors.CustomerDeleted, "This account was deleted.");
        }
    }

    /// <summary>CUS-R2: null leaves a field unchanged.</summary>
    public void UpdateProfile(string? fullName, string? email, bool? marketingOptIn)
    {
        EnsureActive();
        if (fullName is not null)
        {
            FullName = fullName.Trim();
        }

        if (email is not null)
        {
            Email = email.Trim().Length == 0 ? null : email.Trim();
        }

        if (marketingOptIn is { } optIn)
        {
            MarketingOptIn = optIn;
        }
    }

    public void TouchAddresses(DateTimeOffset now) => AddressesChangedAt = now;

    /// <summary>CUS-R7: personal data removed; terminal.</summary>
    public void Delete(DateTimeOffset now)
    {
        EnsureActive();
        Status = CustomerStatus.Deleted;
        FullName = null;
        Email = null;
        MarketingOptIn = false;
        DeletedAt = now;
        Raise(new CustomerDeleted(Id, UserId, now));
    }

    /// <summary>CUS-R8.</summary>
    public void Block()
    {
        if (IsBlocked)
        {
            return;
        }

        IsBlocked = true;
        Raise(new CustomerBlocked(Id, UserId));
    }

    public void Unblock()
    {
        if (Status == CustomerStatus.Deleted)
        {
            throw new ConflictException(CustomersErrors.InvalidStatusTransition, "A deleted account cannot be unblocked.");
        }

        if (!IsBlocked)
        {
            return;
        }

        IsBlocked = false;
        Raise(new CustomerUnblocked(Id, UserId));
    }
}
