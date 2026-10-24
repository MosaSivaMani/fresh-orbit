using FreshOrbit.Cart.Domain.Carts;
using FreshOrbit.Cart.Domain.Shared;
using FreshOrbit.Cart.Public;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Pricing;

namespace FreshOrbit.Cart.Application.Pricing;

/// <summary>
/// <c>GET /price-quote</c>: one line priced before it is in the cart (CART-R17). Same loaders, quantity rules and line
/// formula as a cart line (<see cref="CartPricer"/>, <see cref="QuantityRules"/>, <see cref="LineCalculator"/>), so the
/// quote equals the cart line it becomes (without a coupon).
/// </summary>
/// <param name="Quantity">The quoted (net) quantity.</param>
/// <param name="MaxChargeableQuantity">The most that may be charged after weighing (CART-R7).</param>
/// <param name="PriceUnit">KG, PIECE or PACK: what <paramref name="UnitPricePaise"/> is per.</param>
/// <param name="DiscountBp">Saving against <paramref name="CompareAtPaise"/> (floor); null unless compare-at is above the price.</param>
/// <param name="TaxPaise">GST contained in <paramref name="EstimatedTotalPaise"/>.</param>
/// <param name="MaxChargeablePaise">Line total at <paramref name="MaxChargeableQuantity"/>.</param>
/// <param name="WeightNote">The net-weight note for weight-variable variants; null otherwise.</param>
public sealed record PriceQuoteResponse(
    Guid VariantId,
    string? PreparationCode,
    QuantityView Quantity,
    QuantityView MaxChargeableQuantity,
    long UnitPricePaise,
    long? CompareAtPaise,
    int? DiscountBp,
    string PriceUnit,
    long ItemPaise,
    long PreparationPaise,
    string? ChargeBasis,
    int TaxRateBp,
    long TaxPaise,
    long EstimatedTotalPaise,
    long MaxChargeablePaise,
    CartAvailabilityView Availability,
    string? FulfillmentLocationCode,
    string Pincode,
    string? WeightNote);

public sealed class PriceQuoteService(CartPricer pricer, ILocationsApi locations, ISettingsApi settings)
{
    /// <summary>
    /// Refuses (422) a missing pincode (LOCATION_REQUIRED), an unserved one (PINCODE_NOT_SERVICEABLE), missing
    /// parameters (VALIDATION_FAILED), a product or preparation that cannot be ordered (PRODUCT_NOT_AVAILABLE,
    /// PREPARATION_NOT_AVAILABLE), a wrong unit or quantity (QUANTITY_*) and a missing price or charge
    /// (PRICE_NOT_AVAILABLE, PREPARATION_CHARGE_NOT_AVAILABLE). Stock is reported, not refused.
    /// </summary>
    public async Task<PriceQuoteResponse> QuoteAsync(string? pincode, Guid? variantId, string? preparationCode, int? quantity, string? unit, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();
        if (variantId is null)
        {
            errors.Add(new FieldError("variantId", RefusalException.DefaultCode, "Send the variantId to quote."));
        }

        if (quantity is null)
        {
            errors.Add(new FieldError("quantity", RefusalException.DefaultCode, "Send the quantity to quote."));
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            errors.Add(new FieldError("unit", RefusalException.DefaultCode, "Send the quantity unit: GRAM, PIECE or PACK."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        var trimmed = pincode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException(CartErrors.LocationRequired, "Send your delivery pincode (X-Pincode header or pincode query) for a price quote.", "pincode");
        }

        var serviceability = await locations.ResolvePincodeAsync(trimmed, cancellationToken);
        if (serviceability is not { IsServiceable: true, FulfillmentLocationId: { } locationId, CityId: { } cityId })
        {
            throw new DomainException(CartErrors.PincodeNotServiceable, "We do not deliver to this pincode yet; enter another pincode.", "pincode");
        }

        var variant = (await pricer.LoadVariantsAsync([variantId!.Value], cancellationToken)).GetValueOrDefault(variantId.Value);
        if (variant is not { IsActive: true, ProductStatus: ProductStatus.Active })
        {
            throw new DomainException(CartErrors.ProductNotAvailable, "This product is not available; choose another product.", "variantId");
        }

        PreparationInfo? preparation;
        if (string.IsNullOrWhiteSpace(preparationCode))
        {
            preparation = CartPricer.DefaultPreparation(variant);
        }
        else
        {
            preparation = variant.Preparations.FirstOrDefault(p => p.IsActive && string.Equals(p.Code, preparationCode.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new DomainException(CartErrors.PreparationNotAvailable, "This preparation is not offered for the product; choose another preparation.", "preparation");
        }

        var unitCode = LineCalculator.QuantityUnitCode(variant.SellUnit);
        if (!string.Equals(unit!.Trim(), unitCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new RefusalException(new FieldError("unit", CartErrors.QuantityUnitInvalid, $"Quantity unit must be {unitCode} for this product."));
        }

        if (new QuantityRules(variant.MinOrder, variant.MaxOrder, variant.OrderStep).Check(quantity!.Value, "quantity") is { } quantityError)
        {
            throw new RefusalException(quantityError);
        }

        var resolved = new ResolvedLine(new PricingLine(Guid.Empty, variant.VariantId, preparation?.PreparationMethodId, quantity.Value), variant, preparation, null);
        var prices = await pricer.LoadPricesAsync(locationId, [resolved], at, cancellationToken);
        var stock = await pricer.LoadAvailabilityAsync(locationId, [variant.VariantId], cancellationToken);
        var policy = await settings.GetOrderPolicyAsync(cityId, cancellationToken);
        var line = LineCalculator.Price(resolved, prices.GetValueOrDefault((variant.VariantId, preparation?.PreparationMethodId)), stock.GetValueOrDefault(variant.VariantId), policy.MaxOverweightBp);
        if (line.Price is not { } price)
        {
            var issue = line.Issues[0];
            throw new DomainException(issue.Code, issue.Message, issue.Code == CartErrors.PriceNotAvailable ? "variantId" : "preparation");
        }

        var summaries = await locations.GetFulfillmentLocationsAsync([locationId], cancellationToken);
        var summary = summaries.Count > 0 ? summaries[0] : null;
        return new PriceQuoteResponse(
            variant.VariantId,
            preparation?.Code,
            new QuantityView(quantity.Value, unitCode),
            new QuantityView(line.MaxChargeableQuantity, unitCode),
            price.UnitPricePaise,
            price.CompareAtPaise,
            CompareAtDiscount.Bp(price.UnitPricePaise, price.CompareAtPaise),
            variant.SellUnit.ToCode(),
            line.Estimated.ItemPaise,
            line.Estimated.PreparationPaise,
            price.ChargeBasis?.ToCode(),
            line.TaxRateBp,
            LinePricing.ContainedTax(line.Estimated.TotalPaise, line.TaxRateBp),
            line.Estimated.TotalPaise,
            line.Max.TotalPaise,
            new CartAvailabilityView(line.Availability.Status.ToCode(), line.Availability.Message),
            summary?.Code,
            serviceability.Pincode,
            variant.IsWeightVariable && policy.MaxOverweightBp > 0 ? CartPricer.WeightNote(policy.MaxOverweightBp) : null);
    }
}
