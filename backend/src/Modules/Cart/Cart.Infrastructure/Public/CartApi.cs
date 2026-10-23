using FreshOrbit.Cart.Application.Abstractions;
using FreshOrbit.Cart.Application.Carts;
using FreshOrbit.Cart.Public;

namespace FreshOrbit.Cart.Infrastructure.Public;

/// <summary>Implementation of <see cref="ICartApi"/> on the shared DbContext.</summary>
internal sealed class CartApi(CartService service) : ICartApi
{
    public Task<CheckoutCart?> GetCheckoutCartAsync(Guid customerId, CancellationToken cancellationToken = default)
        => service.GetCheckoutCartAsync(customerId, cancellationToken);

    public Task<CartQuote?> QuoteAsync(CartQuoteRequest request, CancellationToken cancellationToken = default)
        => service.QuoteAsync(request, cancellationToken);

    public Task MarkConvertedAsync(Guid cartId, CancellationToken cancellationToken = default)
        => service.MarkConvertedAsync(cartId, cancellationToken);

    public Task<GuestCartMergeResult> MergeGuestCartAsync(string guestToken, Guid customerId, CancellationToken cancellationToken = default)
        => service.MergeGuestCartAsync(guestToken, customerId, cancellationToken);

    public Task<CartView> AddLinesAsync(Guid customerId, string pincode, IReadOnlyList<CartLineToAdd> lines, CancellationToken cancellationToken = default)
        => service.AddLinesAsync(customerId, pincode, lines, cancellationToken);
}

/// <summary>
/// Stand-in until <c>Pricing.Public</c> exposes the DELIVERY_SERVICE tax rule (merge gap): the delivery fee is treated
/// as carrying no contained tax.
/// </summary>
internal sealed class PricingGapDeliveryFeeTaxRate : IDeliveryFeeTaxRate
{
    public Task<int?> GetRateBpAsync(DateTimeOffset at, CancellationToken cancellationToken) => Task.FromResult<int?>(0);
}
