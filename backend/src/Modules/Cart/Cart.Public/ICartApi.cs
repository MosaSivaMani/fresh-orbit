namespace FreshOrbit.Cart.Public;

/// <summary>What other modules may ask the Cart module (docs/product/cart.md §1).</summary>
public interface ICartApi
{
    /// <summary>The customer's ACTIVE cart lines (no prices), or null when there is none.</summary>
    Task<CheckoutCart?> GetCheckoutCartAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prices the customer's ACTIVE cart for delivery to <see cref="CartQuoteRequest.Pincode"/> at
    /// <see cref="CartQuoteRequest.At"/> (CART-R1..R12): current prices, preparation charges, tax, yield, stock,
    /// order policy, service-area fee and minimum, coupon. Read-only. Null when the customer has no ACTIVE cart.
    /// </summary>
    Task<CartQuote?> QuoteAsync(CartQuoteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Marks the cart CONVERTED in the caller's transaction (tracked; the caller saves).</summary>
    Task MarkConvertedAsync(Guid cartId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges a guest cart into the customer's ACTIVE cart after login (CART-R27..R30), in its own transaction.
    /// Unknown or already merged token → no-op with <c>MergedLineCount = 0</c>.
    /// </summary>
    Task<GuestCartMergeResult> MergeGuestCartAsync(string guestToken, Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds lines to the customer's cart (reorder, ORD-R59) in its own transaction. Each line is checked like an add
    /// (CART-R16); lines that cannot be added are skipped and named in <see cref="CartView.Messages"/>.
    /// </summary>
    /// <param name="pincode">Used when the cart has no location yet (the order's delivery pincode).</param>
    Task<CartView> AddLinesAsync(Guid customerId, string pincode, IReadOnlyList<CartLineToAdd> lines, CancellationToken cancellationToken = default);
}

/// <summary>A cart as checkout needs it: no prices (the server prices on every read).</summary>
public sealed record CheckoutCart(Guid CartId, Guid? FulfillmentLocationId, string? Pincode, string? CouponCode, IReadOnlyList<CheckoutCartLine> Lines);

/// <param name="Quantity">Net quantity in the variant's stock unit (grams or pieces).</param>
public sealed record CheckoutCartLine(Guid LineId, Guid VariantId, Guid? PreparationMethodId, int Quantity);

public sealed record CartLineToAdd(Guid VariantId, Guid? PreparationMethodId, int Quantity);

public sealed record GuestCartMergeResult(Guid? CartId, int MergedLineCount, int DroppedLineCount);

public sealed record CartQuoteRequest(Guid CustomerId, string Pincode, DateTimeOffset At);

/// <summary>
/// A priced cart for checkout. <see cref="View"/> is what the customer sees (same shape as <c>GET /cart</c>);
/// <see cref="Lines"/> carries the snapshot facts an order stores (ORD-R16..R18), in cart line order.
/// </summary>
/// <param name="LocationMatchesCart">False when the pincode resolves to another fulfillment location than the cart's (ORD-R5).</param>
/// <param name="DeliveryFeeTaxRateBp">GST rate contained in the delivery fee (tax category DELIVERY_SERVICE).</param>
public sealed record CartQuote(
    Guid CartId,
    string Pincode,
    Guid FulfillmentLocationId,
    string? FulfillmentLocationCode,
    Guid CityId,
    Guid ServiceAreaId,
    string? TimeZone,
    bool LocationMatchesCart,
    int MaxOverweightBp,
    int DeliveryFeeTaxRateBp,
    string? CouponCode,
    CartView View,
    IReadOnlyList<QuoteLine> Lines);

/// <param name="SellUnit">KG / PIECE / PACK.</param>
/// <param name="StockUnit">GRAM / PIECE.</param>
/// <param name="ChargeBasis">PER_KG / PER_ITEM / FLAT, or null when there is no preparation charge.</param>
/// <param name="PreparationChargeRatePaise">Charge per <paramref name="ChargeBasis"/> unit.</param>
/// <param name="GrossQuantity">Stock to reserve: ceil(net × 10000 / yield) for GRAM, the count for PIECE.</param>
/// <param name="DiscountPaise">Coupon discount allocated to this line (PROMO-R14).</param>
/// <param name="TaxPaise">Tax contained in (estimated total − discount).</param>
public sealed record QuoteLine(
    Guid LineId,
    Guid ProductId,
    Guid CategoryId,
    string ProductName,
    Guid VariantId,
    string VariantName,
    string Sku,
    string? ImageKey,
    string HsnCode,
    string SellUnit,
    string StockUnit,
    Guid? PreparationMethodId,
    string? PreparationCode,
    string? PreparationName,
    int YieldBp,
    bool IsWeightVariable,
    int Quantity,
    int MaxChargeableQuantity,
    int GrossQuantity,
    long UnitPricePaise,
    long PreparationChargeRatePaise,
    string? ChargeBasis,
    int TaxRateBp,
    long ItemPaise,
    long PreparationPaise,
    long EstimatedTotalPaise,
    long MaxLineTotalPaise,
    long DiscountPaise,
    long TaxPaise,
    bool IsSellable,
    IReadOnlyList<CartIssueView> Issues);

/// <summary>A quantity as the API shows it. <paramref name="Unit"/> is GRAM (KG variants), PIECE or PACK.</summary>
public sealed record QuantityView(int Value, string Unit);

public sealed record CartPreparationView(string Code, string Name, int YieldBp);

/// <param name="Status">IN_STOCK / LOW_STOCK / OUT_OF_STOCK / NOT_SOLD_HERE.</param>
public sealed record CartAvailabilityView(string Status, string? Message);

public sealed record CartIssueView(string Code, string Message);

/// <summary>One cart line as the API shows it (docs/api §4.6, §5.1).</summary>
/// <param name="MinOrder">The variant's order rules (the same ones the cart validates quantities with), in the line's
/// quantity unit; null only when the variant no longer exists. Likewise <paramref name="MaxOrder"/> and
/// <paramref name="OrderStep"/>.</param>
public sealed record CartLineView(
    Guid Id,
    Guid ProductId,
    string ProductSlug,
    string ProductName,
    Guid VariantId,
    string VariantName,
    string SellUnit,
    string? ImageUrl,
    CartPreparationView? Preparation,
    QuantityView Quantity,
    QuantityView EstimatedNetQuantity,
    QuantityView MaxChargeableQuantity,
    long? UnitPricePaise,
    long? CompareAtPaise,
    string PriceUnit,
    long ItemPaise,
    long PreparationChargePaise,
    string? PreparationChargeBasis,
    int TaxRateBp,
    long TaxPaise,
    long EstimatedLineTotalPaise,
    long MaxLineTotalPaise,
    CartAvailabilityView Availability,
    bool IsSellable,
    IReadOnlyList<CartIssueView> Issues,
    QuantityView? MinOrder = null,
    QuantityView? MaxOrder = null,
    QuantityView? OrderStep = null);

/// <param name="Status">APPLIED / NOT_APPLICABLE / INVALID.</param>
/// <param name="ReasonCode">COUPON_* reason when not applied.</param>
public sealed record CartCouponView(string Code, string Status, string? ReasonCode, long DiscountPaise, string? Message);

/// <summary>Cart totals (docs/api §5.2).</summary>
public sealed record CartTotalsView(
    long ItemsPaise,
    long PreparationPaise,
    long SubtotalPaise,
    long DiscountPaise,
    long DeliveryFeePaise,
    bool DeliveryFeeWaived,
    long TaxPaise,
    long EstimatedTotalPaise,
    long MaxTotalPaise,
    long MinOrderPaise,
    long MinOrderShortfallPaise,
    long? FreeDeliveryShortfallPaise)
{
    public static CartTotalsView Empty { get; } = new(0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, null);
}

/// <summary>A reason the cart cannot be checked out, using docs/api §2 codes.</summary>
public sealed record CartBlockerView(string Code, Guid? LineId, string Message);

/// <summary>The priced cart (<c>GET /cart</c> and every cart write).</summary>
/// <param name="CartToken">The guest token, only in the response that issued it.</param>
public sealed record CartView(
    Guid? Id,
    string? CartToken,
    string? Pincode,
    string? FulfillmentLocationCode,
    IReadOnlyList<CartLineView> Lines,
    CartCouponView? Coupon,
    CartTotalsView Totals,
    bool CanCheckout,
    IReadOnlyList<CartBlockerView> CheckoutBlockers,
    IReadOnlyList<string> Messages);
