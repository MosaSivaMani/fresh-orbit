namespace FreshOrbit.Cart.Public;

/// <summary>What other modules may ask the Cart module.</summary>
public interface ICartApi
{
    /// <summary>The customer's active cart, or null when there is none.</summary>
    Task<CheckoutCart?> GetCheckoutCartAsync(Guid customerId, CancellationToken cancellationToken = default);
}

/// <summary>A cart as checkout needs it: no prices (the server prices on every read).</summary>
public sealed record CheckoutCart(Guid CartId, Guid? FulfillmentLocationId, string? Pincode, string? CouponCode, IReadOnlyList<CheckoutCartLine> Lines);

/// <param name="Quantity">Net quantity in the variant's stock unit (grams or pieces).</param>
public sealed record CheckoutCartLine(Guid VariantId, Guid? PreparationMethodId, int Quantity);
