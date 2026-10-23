using FreshOrbit.Cart.Public;
using FreshOrbit.Ordering.Application.Orders;

namespace FreshOrbit.Ordering.Application.Checkout;

/// <summary>Body of <c>POST /checkout/preview</c> and <c>POST /orders</c> (§4.8); no lines or prices.</summary>
public sealed record CheckoutRequest(
    Guid AddressId,
    Guid DeliverySlotId,
    string? PaymentMethod,
    string? SubstitutionPreference,
    string? CustomerNotes,
    long? ExpectedTotalPaise);

public sealed record CheckoutAddressDto(Guid Id, string? Label, string Line1, string Locality, string Pincode, bool IsServiceable);

public sealed record CheckoutSlotDto(Guid Id, DateTimeOffset? StartAt, DateTimeOffset? EndAt, DateTimeOffset? CutoffAt, string Status);

public sealed record PaymentMethodOption(string Method, bool Enabled, string? ReasonCode);

/// <summary>Checkout preview (§4.8, ORD-R15): business problems are <c>blockers</c>, never 422.</summary>
public sealed record CheckoutPreview(
    Guid? CartId,
    CheckoutAddressDto Address,
    string? FulfillmentLocationCode,
    CheckoutSlotDto Slot,
    IReadOnlyList<CartLineView> Lines,
    CartCouponView? Coupon,
    CartTotalsView Totals,
    IReadOnlyList<PaymentMethodOption> PaymentMethods,
    bool CanPlaceOrder,
    IReadOnlyList<CartBlockerView> Blockers,
    long ExpectedTotalPaise);

/// <summary>Outcome of placing an order: the order, or PRICE_CHANGED with the new preview (ORD-R10).</summary>
public sealed record PlaceOrderOutcome(OrderDetailResponse? Order, CheckoutPreview? PriceChangedPreview, bool Replayed);
