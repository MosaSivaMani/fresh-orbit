using FreshOrbit.Catalog.Public;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.Pricing.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Pricing;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Ordering.Application.Orders;

/// <summary>
/// Builds a fresh line snapshot for a proposed substitute at the order's location and current price (ORD-R33, R35),
/// with the same line formula as cart and checkout (D20).
/// </summary>
public sealed class SubstitutionPricer(ICatalogApi catalog, IPricingApi pricing)
{
    public const string PriceNotAvailable = "PRICE_NOT_AVAILABLE";

    /// <summary>The snapshot for <paramref name="quantity"/> of the variant and preparation, with a new line id.</summary>
    public async Task<(OrderItemSnapshot Snapshot, string Name)> PriceAsync(
        Order order,
        Guid itemId,
        Guid variantId,
        Guid? preparationMethodId,
        int quantity,
        int maxOverweightBp,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        var variant = (await catalog.GetVariantsAsync([variantId], cancellationToken)).FirstOrDefault(v => v.VariantId == variantId);
        if (variant is not { IsActive: true })
        {
            throw new DomainException(OrderingErrors.SubstitutionNotAllowed, "The proposed product is not available.", "variantId");
        }

        var preparation = preparationMethodId is { } prepId
            ? variant.Preparations.FirstOrDefault(p => p.PreparationMethodId == prepId && p.IsActive)
                ?? throw new DomainException(OrderingErrors.SubstitutionNotAllowed, "The preparation is not offered for this product.", "preparationMethodId")
            : variant.Preparations.FirstOrDefault(p => p.IsDefault && p.IsActive);

        if (quantity <= 0 || quantity < variant.MinOrder || quantity > variant.MaxOrder || (variant.OrderStep > 0 && quantity % variant.OrderStep != 0))
        {
            throw new DomainException(OrderingErrors.ValidationFailed, $"Quantity must be {variant.MinOrder}–{variant.MaxOrder} in steps of {variant.OrderStep}.", "quantity.value");
        }

        var price = (await pricing.GetLinePricesAsync(order.FulfillmentLocationId, [new PriceKey(variantId, preparation?.PreparationMethodId)], at, cancellationToken))
            .FirstOrDefault(p => p.VariantId == variantId)
            ?? throw new DomainException(PriceNotAvailable, "The proposed product has no price at this location.", "variantId");

        var sellUnit = variant.SellUnit.ToCode();
        var basisCode = price.ChargeBasis?.ToCode();
        var facts = new LinePriceFacts(OrderUnits.PricedUnitOf(sellUnit), price.UnitPricePaise, price.PreparationChargePaise, OrderUnits.BasisOf(basisCode), price.TaxRateBp);
        if (!LinePricing.IsSupported(facts.Unit, facts.ChargeBasis))
        {
            throw new DomainException(PriceNotAvailable, "The preparation charge cannot be applied to this product.", "preparationMethodId");
        }

        var amounts = LinePricing.Compute(facts, quantity);
        var maxQuantity = LinePricing.MaxChargeableQuantity(quantity, variant.IsWeightVariable, maxOverweightBp);
        var max = LinePricing.Compute(facts, maxQuantity);
        var yieldBp = variant.StockUnit == StockUnit.Gram && preparation is { YieldBp: > 0 } ? preparation.YieldBp : BasisPoints.Whole;
        var gross = variant.StockUnit == StockUnit.Gram ? Weight.GrossForNet(quantity, yieldBp) : quantity;
        var snapshot = new OrderItemSnapshot(
            itemId,
            variant.ProductId,
            variant.CategoryId,
            variant.VariantId,
            variant.ProductName,
            variant.VariantName,
            variant.Sku,
            variant.ImageKey,
            variant.HsnCode,
            preparation?.PreparationMethodId,
            preparation?.Code,
            preparation?.Name,
            sellUnit,
            variant.StockUnit.ToCode(),
            price.UnitPricePaise,
            price.PreparationChargePaise,
            basisCode,
            price.TaxRateBp,
            yieldBp,
            variant.IsWeightVariable,
            quantity,
            gross,
            maxQuantity,
            amounts.ItemPaise,
            amounts.PreparationPaise,
            amounts.TotalPaise,
            max.TotalPaise,
            0,
            LinePricing.ContainedTax(amounts.TotalPaise, price.TaxRateBp));
        var name = preparation is null ? $"{variant.ProductName} – {variant.VariantName}" : $"{variant.ProductName} – {variant.VariantName} – {preparation.Name}";
        return (snapshot, name);
    }
}
