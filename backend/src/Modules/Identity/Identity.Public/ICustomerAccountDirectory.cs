namespace FreshOrbit.Identity.Public;

/// <summary>
/// Implemented by the Customers module (which uses Identity): the customer record behind a customer login.
/// Identity calls it during OTP login (customer creation, JWT <c>customer_id</c>) and Notifications uses it to find the
/// user of a customer. Writes are tracked on the shared DbContext and saved by the caller.
/// </summary>
public interface ICustomerAccountDirectory
{
    /// <summary>The customer of <paramref name="userId"/>, created (ACTIVE, tracked, not saved) when it does not exist.</summary>
    Task<CustomerAccount> EnsureCustomerAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Guid?> FindCustomerIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Guid?> FindUserIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>E-mail addresses on the customers' profiles by user id (users without one are absent).</summary>
    Task<IReadOnlyDictionary<Guid, string>> FindEmailsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}

public sealed record CustomerAccount(Guid CustomerId, bool IsNew, string? FullName, bool IsDeleted);
