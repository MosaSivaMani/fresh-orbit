using FreshOrbit.Customers.Domain.Addresses;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Customers.Domain.StockAlerts;
using FreshOrbit.Customers.Domain.Wishlist;

namespace FreshOrbit.Customers.Domain.Shared;

public interface ICustomersRepository
{
    Task<Customer?> GetAsync(Guid customerId, CancellationToken cancellationToken);

    Task<Customer?> FindByUserAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Customer customer);

    /// <summary>Active addresses, default first then newest first.</summary>
    Task<IReadOnlyList<Address>> GetActiveAddressesAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Every address of the customer, active or removed (account deletion anonymises them all).</summary>
    Task<IReadOnlyList<Address>> GetAllAddressesAsync(Guid customerId, CancellationToken cancellationToken);

    Task<Address?> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken);

    void Add(Address address);

    Task<WishlistItem?> GetWishlistItemAsync(Guid customerId, Guid productId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WishlistItem>> GetWishlistAsync(Guid customerId, CancellationToken cancellationToken);

    void Add(WishlistItem item);

    void Remove(WishlistItem item);

    /// <summary>The customer's alerts, newest first.</summary>
    Task<IReadOnlyList<StockAlert>> GetStockAlertsAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Every alert waiting for the variant at the location (all customers).</summary>
    Task<IReadOnlyList<StockAlert>> GetStockAlertsForAsync(Guid variantId, Guid fulfillmentLocationId, CancellationToken cancellationToken);

    void Add(StockAlert alert);

    void Remove(StockAlert alert);

    /// <summary>Writes pending changes so later queries in the same transaction see them (e.g. the default flag moved).</summary>
    Task FlushAsync(CancellationToken cancellationToken);

    /// <summary>Runs <paramref name="work"/> in one database transaction (joins an open one).</summary>
    Task<TResult> InTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken cancellationToken);

    /// <summary>True when <paramref name="exception"/> is a unique-key violation from a save.</summary>
    bool IsDuplicateKey(Exception exception);

    /// <summary>Admin list: <paramref name="userIds"/> narrows to those users when not null.</summary>
    Task<(IReadOnlyList<Customer> Items, int TotalCount)> SearchAsync(string? name, IReadOnlyCollection<Guid>? userIds, CustomerStatus? status, int skip, int take, CancellationToken cancellationToken);
}
