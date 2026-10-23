using System.Security.Cryptography;
using System.Text;

namespace FreshOrbit.Cart.Application.Carts;

/// <summary>A quantity as the client sends it: <c>{ "value": 500, "unit": "GRAM" }</c>.</summary>
public sealed record QuantityRequest(int Value, string Unit);

/// <summary>Body of <c>POST /cart/items</c>. <see cref="PreparationCode"/> omitted → the variant's default.</summary>
public sealed record AddCartItemRequest(Guid VariantId, string? PreparationCode, QuantityRequest Quantity);

/// <summary>Body of <c>PATCH /cart/items/{itemId}</c>: change the quantity, the preparation, or both.</summary>
public sealed record ChangeCartItemRequest(QuantityRequest? Quantity, string? PreparationCode);

/// <summary>Body of <c>PUT /cart/coupon</c>.</summary>
public sealed record CartCouponRequest(string Code);

/// <summary>Body of <c>PUT /cart/location</c>.</summary>
public sealed record CartLocationRequest(string Pincode);

/// <summary>Who owns the cart: a customer JWT wins over a guest token (CART-R21).</summary>
public sealed record CartOwner(Guid? CustomerId, string? GuestToken)
{
    public bool IsCustomer => CustomerId is not null;

    public static CartOwner For(Guid? customerId, string? guestToken)
        => customerId is not null ? new CartOwner(customerId, null) : new CartOwner(null, string.IsNullOrWhiteSpace(guestToken) ? null : guestToken.Trim());
}

/// <summary>A line that would be dropped by a location change (409 CART_LOCATION_CHANGED member <c>lines</c>).</summary>
public sealed record DroppedLine(Guid Id, string Code);

/// <summary>Guest cart tokens: 32 random bytes (base64url) given to the client once; only the SHA-256 is stored (CART-R22).</summary>
public static class GuestTokens
{
    public static string Create() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
