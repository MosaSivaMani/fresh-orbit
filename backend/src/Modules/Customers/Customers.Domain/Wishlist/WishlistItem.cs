namespace FreshOrbit.Customers.Domain.Wishlist;

/// <summary>A product on a customer's wishlist (CUS-R6). Key (customer_id, product_id); insert / delete only.</summary>
public sealed class WishlistItem
{
    private WishlistItem()
    {
    }

    public Guid CustomerId { get; private set; }

    public Guid ProductId { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public static WishlistItem Create(Guid customerId, Guid productId, DateTimeOffset now)
        => new() { CustomerId = customerId, ProductId = productId, AddedAt = now };
}
