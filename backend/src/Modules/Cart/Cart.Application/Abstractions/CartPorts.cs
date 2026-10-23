using FreshOrbit.Cart.Domain.Carts;

namespace FreshOrbit.Cart.Application.Abstractions;

public interface ICartRepository
{
    Task<ShoppingCart?> FindActiveByCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>The cart of a guest token hash in any status (merge needs to see CONVERTED carts).</summary>
    Task<ShoppingCart?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<ShoppingCart?> FindByIdAsync(Guid cartId, CancellationToken cancellationToken);

    /// <summary>Up to <paramref name="take"/> ACTIVE carts whose expiry has passed (tracked).</summary>
    Task<IReadOnlyList<ShoppingCart>> ListDueForAbandonAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);

    void Add(ShoppingCart cart);

    /// <summary>
    /// The command's single save. A cart whose lines changed is saved with its row version even when no cart column
    /// changed, so concurrent line edits collide (CART-R19).
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Runs a cart write (Read → Decide → Commit) and repeats it from Read after a lost race: a
/// <c>ConcurrentSaveException</c> or a unique-index race on the customer's ACTIVE cart, up to 3 retries, then
/// 409 CONCURRENT_SAVE (CART-R19). Each attempt starts with an empty change tracker.
/// </summary>
public interface ICartCommandRunner
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> command, CancellationToken cancellationToken);
}

/// <summary>
/// GST rate contained in the delivery fee (tax category DELIVERY_SERVICE, decision D5); null means the rule is missing
/// (blocker TAX_RULE_MISSING). Adapter <see cref="PricingDeliveryFeeTaxRate"/> over <c>Pricing.Public</c>.
/// </summary>
public interface IDeliveryFeeTaxRate
{
    Task<int?> GetRateBpAsync(DateTimeOffset at, CancellationToken cancellationToken);
}
