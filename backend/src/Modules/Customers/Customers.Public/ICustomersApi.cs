namespace FreshOrbit.Customers.Public;

/// <summary>What other modules may ask the Customers module.</summary>
public interface ICustomersApi
{
    Task<CustomerSummary?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>The customer's active address, or null when it does not exist or belongs to someone else.</summary>
    Task<DeliveryAddress?> GetDeliveryAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken = default);

    /// <summary>The customer of a login user, or null.</summary>
    Task<Guid?> FindCustomerIdByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implemented by modules that can block deleting a customer account (e.g. Ordering with active orders).
/// </summary>
public interface ICustomerDeletionCheck
{
    /// <summary>UPPER_SNAKE codes of what blocks the deletion (e.g. ACTIVE_ORDERS_EXIST); empty when it may proceed.</summary>
    Task<IReadOnlyList<string>> GetBlockingItemsAsync(Guid customerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Blocking items that are "open matters" of the customer (complaints, refunds): deletion is refused with 422
/// <c>ACCOUNT_HAS_OPEN_MATTERS</c> and the kinds as field errors under <c>account</c>, so the customer can be told to wait
/// until they are closed (DPDP: the account is kept while we still owe or decide something).
/// </summary>
public static class CustomerOpenMatters
{
    /// <summary>A complaint that is OPEN or APPROVED (not yet rejected or resolved).</summary>
    public const string OpenComplaint = "OPEN_COMPLAINT";

    /// <summary>A refund that is REQUESTED, APPROVED or PROCESSING (money not yet back, e.g. a COD refund awaiting payout).</summary>
    public const string OpenRefund = "OPEN_REFUND";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal) { OpenComplaint, OpenRefund };
}

/// <summary>Stored as ACTIVE / DELETED.</summary>
public enum CustomerStatus
{
    Active,
    Deleted,
}

/// <param name="FullName">Empty when the customer has not entered a name yet.</param>
public sealed record CustomerSummary(Guid CustomerId, Guid UserId, string FullName, string? Phone, string? Email, CustomerStatus Status);

public sealed record DeliveryAddress(
    Guid AddressId,
    string RecipientName,
    string Phone,
    string Line1,
    string? Line2,
    string? Landmark,
    string Locality,
    string CityName,
    string StateCode,
    string Pincode,
    decimal? Latitude,
    decimal? Longitude);
