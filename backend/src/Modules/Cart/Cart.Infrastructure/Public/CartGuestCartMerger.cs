using FreshOrbit.Cart.Application.Carts;
using FreshOrbit.Identity.Public;

namespace FreshOrbit.Cart.Infrastructure.Public;

/// <summary>
/// Identity's login-time guest cart merge (<see cref="IGuestCartMerger"/>): the same merge as the
/// <c>CustomerSignedIn</c> handler (CART-R27..R30), in its own transaction, so the login answer carries the cart.
/// </summary>
internal sealed class CartGuestCartMerger(CartService service) : IGuestCartMerger
{
    public async Task<GuestCartMerge?> MergeAsync(string guestCartToken, Guid customerId, CancellationToken cancellationToken = default)
    {
        var result = await service.MergeGuestCartAsync(guestCartToken, customerId, cancellationToken);
        return result.CartId is { } cartId ? new GuestCartMerge(cartId, result.MergedLineCount, result.DroppedLineCount) : null;
    }
}
