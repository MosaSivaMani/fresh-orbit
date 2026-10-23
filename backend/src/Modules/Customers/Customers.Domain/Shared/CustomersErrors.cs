namespace FreshOrbit.Customers.Domain.Shared;

/// <summary>Error codes of the Customers module (docs/api §2 Customers).</summary>
public static class CustomersErrors
{
    public const string CustomerDeleted = "CUSTOMER_DELETED";
    public const string AddressLimitReached = "ADDRESS_LIMIT_REACHED";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";

    /// <summary>CUS-R3.</summary>
    public const int MaxActiveAddresses = 20;
}
