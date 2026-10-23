using System.Globalization;
using FreshOrbit.Cart.Application.Abstractions;
using FreshOrbit.Cart.Domain.Shared;
using FreshOrbit.Cart.Public;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Ordering.Public;
using FreshOrbit.Pricing.Public;
using FreshOrbit.Promotions.Public;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Pricing;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Cart.Application.Pricing;

/// <summary>Where the cart is priced: the cart's (or the delivery address's) resolved pincode.</summary>
/// <param name="IsServiceable">False when the pincode no longer resolves; lines are still priced at the location.</param>
public sealed record PricingLocation(
    Guid FulfillmentLocationId,
    string? FulfillmentLocationCode,
    Guid? CityId,
    Guid? ServiceAreaId,
    string Pincode,
    string? TimeZone,
    long DeliveryFeePaise,
    long? FreeDeliveryAbovePaise,
    long MinOrderPaise,
    bool IsServiceable);

public sealed record PricingLine(Guid LineId, Guid VariantId, Guid? PreparationMethodId, int Quantity);

public sealed record PricingRequest(
    Guid? CartId,
    PricingLocation? Location,
    IReadOnlyList<PricingLine> Lines,
    string? CouponCode,
    Guid? CustomerId,
    DateTimeOffset At);

/// <summary>A priced cart: the customer view plus the snapshot facts per line (for checkout).</summary>
public sealed record PricedCart(CartView View, IReadOnlyList<QuoteLine> Lines, int MaxOverweightBp, int DeliveryFeeTaxRateBp);

/// <summary>
/// Prices cart lines on every read (CART-R1..R14) with one batched call per dependency (CART-R12): Catalog
/// (variants), Pricing (price + preparation charge + tax rate at the location and time), Inventory (available),
/// Settings (order policy), Promotions (coupon) and Ordering (first-order flag, only when a coupon is set).
/// No writes.
/// </summary>
public sealed class CartPricer(
    ICatalogApi catalog,
    IPricingApi pricing,
    IInventoryApi inventory,
    ISettingsApi settings,
    IPromotionsApi promotions,
    IOrderingApi ordering,
    IDeliveryFeeTaxRate deliveryFeeTax)
{
    /// <summary>The net-weight note shown with weight-based items.</summary>
    public static string WeightNote(int maxOverweightBp)
        => $"Weight-based items are charged on actual net weight, up to {(maxOverweightBp / 100m).ToString("0.##", CultureInfo.InvariantCulture)}% over the ordered weight.";

    public async Task<PricedCart> PriceAsync(PricingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Location is not { } location || request.Lines.Count == 0)
        {
            return Empty(request);
        }

        var variants = await LoadVariantsAsync([.. request.Lines.Select(l => l.VariantId)], cancellationToken);
        var resolved = request.Lines.Select(l => Resolve(l, variants.GetValueOrDefault(l.VariantId))).ToList();
        var prices = await LoadPricesAsync(location.FulfillmentLocationId, resolved, request.At, cancellationToken);
        var stock = await LoadAvailabilityAsync(location.FulfillmentLocationId, [.. variants.Keys], cancellationToken);
        var policy = await settings.GetOrderPolicyAsync(location.CityId, cancellationToken);
        var feeTaxRate = await deliveryFeeTax.GetRateBpAsync(request.At, cancellationToken);

        var lines = resolved
            .Select(r => LineCalculator.Price(
                r,
                r.Variant is null ? null : prices.GetValueOrDefault((r.Variant.VariantId, r.Preparation?.PreparationMethodId)),
                stock.GetValueOrDefault(r.Line.VariantId),
                policy.MaxOverweightBp))
            .ToList();

        var coupon = await EvaluateCouponAsync(request, location, lines, cancellationToken);
        return Assemble(request, location, lines, coupon, policy.MaxOverweightBp, feeTaxRate);
    }

    /// <summary>Variants by id (one Catalog call).</summary>
    public async Task<Dictionary<Guid, VariantInfo>> LoadVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken)
        => variantIds.Count == 0
            ? []
            : (await catalog.GetVariantsAsync([.. variantIds.Distinct()], cancellationToken)).ToDictionary(v => v.VariantId);

    /// <summary>Current prices by (variant, preparation) at a location (one Pricing call).</summary>
    public async Task<Dictionary<(Guid VariantId, Guid? PreparationMethodId), LinePrice>> LoadPricesAsync(
        Guid fulfillmentLocationId,
        IReadOnlyList<ResolvedLine> lines,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var keys = lines.Where(l => l.Variant is not null).Select(l => new PriceKey(l.Line.VariantId, l.Preparation?.PreparationMethodId)).Distinct().ToList();
        if (keys.Count == 0)
        {
            return [];
        }

        var prices = await pricing.GetLinePricesAsync(fulfillmentLocationId, keys, at, cancellationToken);
        return prices.GroupBy(p => (p.VariantId, p.PreparationMethodId)).ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>Available stock by variant at a location (one Inventory call).</summary>
    public async Task<Dictionary<Guid, VariantAvailability>> LoadAvailabilityAsync(Guid fulfillmentLocationId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken)
        => variantIds.Count == 0
            ? []
            : (await inventory.GetAvailabilityAsync(fulfillmentLocationId, [.. variantIds.Distinct()], cancellationToken)).ToDictionary(a => a.VariantId);

    /// <summary>The line's preparation: the stored one when still enabled, else the variant default when none was stored.</summary>
    public static ResolvedLine Resolve(PricingLine line, VariantInfo? variant)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (variant is null)
        {
            return new ResolvedLine(line, null, null, CartErrors.ProductNotAvailable);
        }

        if (line.PreparationMethodId is { } preparationId)
        {
            var preparation = variant.Preparations.FirstOrDefault(p => p.PreparationMethodId == preparationId);
            return preparation is { IsActive: true }
                ? new ResolvedLine(line, variant, preparation, null)
                : new ResolvedLine(line, variant, preparation, CartErrors.PreparationNotAvailable);
        }

        return new ResolvedLine(line, variant, DefaultPreparation(variant), null);
    }

    public static PreparationInfo? DefaultPreparation(VariantInfo variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        return variant.Preparations.FirstOrDefault(p => p.IsDefault && p.IsActive) ?? variant.Preparations.FirstOrDefault(p => p.IsActive);
    }

    private async Task<CouponEvaluation?> EvaluateCouponAsync(PricingRequest request, PricingLocation location, List<PricedLine> lines, CancellationToken cancellationToken)
    {
        if (request.CouponCode is not { Length: > 0 } code || request.CustomerId is not { } customerId || location.CityId is not { } cityId)
        {
            return null;
        }

        var hasQualifyingOrder = await ordering.HasQualifyingOrderAsync(customerId, cancellationToken);
        var couponLines = lines
            .Where(l => l.IsSellable)
            .Select(l => new CouponLine(l.Line.LineId, l.Variant!.ProductId, l.Variant.CategoryId, l.Estimated.TotalPaise))
            .ToList();
        return await promotions.EvaluateAsync(new EvaluateCouponRequest(code, customerId, cityId, hasQualifyingOrder, request.At, couponLines), cancellationToken);
    }

    private static PricedCart Empty(PricingRequest request)
    {
        var location = request.Location;
        var blockers = new List<CartBlockerView> { new(CartErrors.CartEmpty, null, "Add an item to your cart to check out.") };
        var totals = location is null
            ? CartTotalsView.Empty
            : CartTotalsView.Empty with
            {
                MinOrderPaise = location.MinOrderPaise,
                MinOrderShortfallPaise = location.MinOrderPaise,
                FreeDeliveryShortfallPaise = location.FreeDeliveryAbovePaise,
            };
        var view = new CartView(request.CartId, null, location?.Pincode, location?.FulfillmentLocationCode, [], null, totals, false, blockers, []);
        return new PricedCart(view, [], 0, 0);
    }

    private static PricedCart Assemble(
        PricingRequest request,
        PricingLocation location,
        List<PricedLine> lines,
        CouponEvaluation? coupon,
        int maxOverweightBp,
        int? feeTaxRate)
    {
        var sellable = lines.Where(l => l.IsSellable).ToList();
        var items = Money.Sum(sellable.Select(l => l.Estimated.ItemPaise));
        var preparation = Money.Sum(sellable.Select(l => l.Estimated.PreparationPaise));
        var subtotal = items + preparation;
        var maxSubtotal = Money.Sum(sellable.Select(l => l.Max.TotalPaise));
        var applied = coupon is { Status: CouponApplicationStatus.Applied };
        var discount = applied ? Math.Min(coupon!.DiscountPaise, subtotal) : 0;
        var allocations = applied ? coupon!.Allocations.ToDictionary(a => a.LineRef, a => a.DiscountPaise) : [];
        var afterDiscount = subtotal - discount;

        var waivedByCoupon = applied && coupon!.WaivesDeliveryFee;
        var waivedByThreshold = location.FreeDeliveryAbovePaise is { } threshold && afterDiscount >= threshold;
        var fee = waivedByCoupon || waivedByThreshold ? 0 : location.DeliveryFeePaise;
        var feeWaived = location.DeliveryFeePaise > 0 && (waivedByCoupon || waivedByThreshold);

        var quoteLines = new List<QuoteLine>(lines.Count);
        var lineViews = new List<CartLineView>(lines.Count);
        foreach (var line in lines)
        {
            var lineDiscount = line.IsSellable ? allocations.GetValueOrDefault(line.Line.LineId) : 0;
            var tax = line.IsSellable ? LinePricing.ContainedTax(line.Estimated.TotalPaise - lineDiscount, line.TaxRateBp) : 0;
            lineViews.Add(line.ToView(tax));
            if (line.Variant is not null)
            {
                quoteLines.Add(line.ToQuote(lineDiscount, tax));
            }
        }

        var feeTax = LinePricing.ContainedTax(fee, feeTaxRate ?? 0);
        var taxTotal = Money.Sum(lineViews.Select(l => l.TaxPaise)) + feeTax;
        var minShortfall = Math.Max(0, location.MinOrderPaise - afterDiscount);
        long? freeShortfall = location.FreeDeliveryAbovePaise is { } freeAbove ? Math.Max(0, freeAbove - afterDiscount) : null;
        var totals = new CartTotalsView(
            items,
            preparation,
            subtotal,
            discount,
            fee,
            feeWaived,
            taxTotal,
            afterDiscount + fee,
            maxSubtotal - discount + fee,
            location.MinOrderPaise,
            minShortfall,
            freeShortfall);

        var blockers = new List<CartBlockerView>();
        if (!location.IsServiceable)
        {
            blockers.Add(new CartBlockerView(CartErrors.PincodeNotServiceable, null, "We no longer deliver to this pincode; change the delivery pincode."));
        }

        blockers.AddRange(lines.Where(l => !l.IsSellable).Select(l => new CartBlockerView(l.Issues[0].Code, l.Line.LineId, l.Issues[0].Message)));
        if (minShortfall > 0)
        {
            blockers.Add(new CartBlockerView(CartErrors.MinOrderNotMet, null, $"Add items worth ₹{Money.ToRupeeString(minShortfall)} more to reach the minimum order."));
        }

        if (coupon is { Status: CouponApplicationStatus.Invalid })
        {
            blockers.Add(new CartBlockerView(coupon.RefusalCode ?? "COUPON_INVALID", null, CouponMessage(coupon.RefusalCode)));
        }

        if (feeTaxRate is null && fee > 0)
        {
            blockers.Add(new CartBlockerView(CartErrors.TaxRuleMissing, null, "Delivery cannot be priced right now; try again later."));
        }

        var messages = new List<string>();
        if (sellable.Any(l => l.Variant!.IsWeightVariable) && maxOverweightBp > 0)
        {
            messages.Add(WeightNote(maxOverweightBp));
        }

        messages.AddRange(lines.Where(l => l.Availability.Status == LineAvailability.LowStock).Select(l => $"{l.Variant?.ProductName}: {l.Availability.Message}"));

        var couponView = coupon is null
            ? null
            : new CartCouponView(
                coupon.Code,
                coupon.Status.ToCode(),
                coupon.RefusalCode,
                applied ? discount : 0,
                coupon.Status == CouponApplicationStatus.Applied ? null : CouponMessage(coupon.RefusalCode));
        var view = new CartView(
            request.CartId,
            null,
            location.Pincode,
            location.FulfillmentLocationCode,
            lineViews,
            couponView,
            totals,
            blockers.Count == 0,
            blockers,
            messages);
        return new PricedCart(view, quoteLines, maxOverweightBp, feeTaxRate ?? 0);
    }

    public static string CouponMessage(string? code) => code switch
    {
        "COUPON_EXPIRED" => "This coupon has expired.",
        "COUPON_NOT_STARTED" => "This coupon is not valid yet.",
        "COUPON_LIMIT_REACHED" => "This coupon has reached its usage limit.",
        "COUPON_FIRST_ORDER_ONLY" => "This coupon is for your first order only.",
        "COUPON_MIN_ORDER_NOT_MET" => "Add more eligible items to use this coupon.",
        _ => "This coupon cannot be used for this cart.",
    };
}

/// <summary>A cart line with its variant and preparation looked up; <see cref="Issue"/> when either is unusable.</summary>
public sealed record ResolvedLine(PricingLine Line, VariantInfo? Variant, PreparationInfo? Preparation, string? Issue);
