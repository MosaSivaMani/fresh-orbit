namespace FreshOrbit.Customers.Public;

/// <summary>What other modules may ask the Customers module.</summary>
public interface ICustomersApi
{
    Task<CustomerSummary?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>The customer's active address, or null when it does not exist or belongs to someone else.</summary>
    Task<DeliveryAddress?> GetDeliveryAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken = default);
}

/// <summary>Stored as ACTIVE / DELETED.</summary>
public enum CustomerStatus
{
    Active,
    Deleted,
}

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
