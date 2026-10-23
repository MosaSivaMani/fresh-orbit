using System.Globalization;
using FreshOrbit.Cart.Domain.Shared;
using FreshOrbit.Cart.Public;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Pricing.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Pricing;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Cart.Application.Pricing;

/// <summary>Computed (never stored) availability of a line (cart.md §2).</summary>
public enum LineAvailability
{
    InStock,
    LowStock,
    OutOfStock,
    NotSoldHere,
}

public sealed record AvailabilityResult(LineAvailability Status, string? Message);

/// <summary>One line priced at the cart's location: amounts at the ordered and max chargeable quantities, issues.</summary>
public sealed record PricedLine(
    PricingLine Line,
    VariantInfo? Variant,
    PreparationInfo? Preparation,
    LinePrice? Price,
    int GrossQuantity,
    int MaxChargeableQuantity,
    LineAmounts Estimated,
    LineAmounts Max,
    AvailabilityResult Availability,
    IReadOnlyList<CartIssueView> Issues)
{
    public bool IsSellable => Issues.Count == 0;

    public int TaxRateBp => Price?.TaxRateBp ?? 0;

    public CartLineView ToView(long taxPaise)
    {
        var sellUnit = Variant?.SellUnit ?? SellUnit.Kg;
        var unit = LineCalculator.QuantityUnitCode(sellUnit);
        return new CartLineView(
            Line.LineId,
            Variant?.ProductId ?? Guid.Empty,
            Variant?.ProductSlug ?? string.Empty,
            Variant?.ProductName ?? string.Empty,
            Line.VariantId,
            Variant?.VariantName ?? string.Empty,
            sellUnit.ToCode(),
            Variant?.ImageKey is { } key ? $"/media/{key}" : null,
            Preparation is null ? null : new CartPreparationView(Preparation.Code, Preparation.Name, Preparation.YieldBp),
            new QuantityView(Line.Quantity, unit),
            new QuantityView(Line.Quantity, unit),
            new QuantityView(MaxChargeableQuantity, unit),
            Price?.UnitPricePaise,
            Price?.CompareAtPaise,
            sellUnit.ToCode(),
            Estimated.ItemPaise,
            Estimated.PreparationPaise,
            Price?.ChargeBasis?.ToCode(),
            TaxRateBp,
            taxPaise,
            Estimated.TotalPaise,
            Max.TotalPaise,
            new CartAvailabilityView(Availability.Status.ToCode(), Availability.Message),
            IsSellable,
            Issues);
    }

    public QuoteLine ToQuote(long discountPaise, long taxPaise)
    {
        var variant = Variant ?? throw new InvalidOperationException("A quote line needs its variant.");
        return new QuoteLine(
            Line.LineId,
            variant.ProductId,
            variant.CategoryId,
            variant.ProductName,
            variant.VariantId,
            variant.VariantName,
            variant.Sku,
            variant.ImageKey,
            variant.HsnCode,
            variant.SellUnit.ToCode(),
            variant.StockUnit.ToCode(),
            Preparation?.PreparationMethodId,
            Preparation?.Code,
            Preparation?.Name,
            LineCalculator.YieldOf(variant, Preparation),
            variant.IsWeightVariable,
            Line.Quantity,
            MaxChargeableQuantity,
            GrossQuantity,
            Price?.UnitPricePaise ?? 0,
            Price?.PreparationChargePaise ?? 0,
            Price?.ChargeBasis?.ToCode(),
            TaxRateBp,
            Estimated.ItemPaise,
            Estimated.PreparationPaise,
            Estimated.TotalPaise,
            Max.TotalPaise,
            discountPaise,
            taxPaise,
            IsSellable,
            Issues);
    }
}

/// <summary>Pure per-line rules: CART-R3..R9, R13, R14 and the line issues of §5.1.</summary>
public static class LineCalculator
{
    public static PricedLine Price(ResolvedLine resolved, LinePrice? price, VariantAvailability? stock, int maxOverweightBp)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        var line = resolved.Line;
        var variant = resolved.Variant;
        var issues = new List<CartIssueView>();
        if (variant is null || !variant.IsActive)
        {
            issues.Add(new CartIssueView(CartErrors.ProductNotAvailable, "This product is no longer available; remove it from your cart."));
            return Unpriced(resolved, issues);
        }

        if (resolved.Issue == CartErrors.PreparationNotAvailable)
        {
            issues.Add(new CartIssueView(CartErrors.PreparationNotAvailable, "This preparation is no longer offered; choose another preparation."));
            return Unpriced(resolved, issues);
        }

        if (price is null)
        {
            issues.Add(new CartIssueView(CartErrors.PriceNotAvailable, "This item is not sold at your location; remove it from your cart."));
            return Unpriced(resolved, issues);
        }

        var facts = FactsOf(variant, price);
        if (!LinePricing.IsSupported(facts.Unit, facts.ChargeBasis))
        {
            issues.Add(new CartIssueView(CartErrors.PreparationChargeNotAvailable, "This preparation cannot be priced right now; choose another preparation."));
            return Unpriced(resolved, issues);
        }

        var yieldBp = YieldOf(variant, resolved.Preparation);
        var gross = GrossQuantity(variant.StockUnit, line.Quantity, yieldBp);
        var maxChargeable = LinePricing.MaxChargeableQuantity(line.Quantity, variant.IsWeightVariable, maxOverweightBp);
        var estimated = LinePricing.Compute(facts, line.Quantity);
        var max = LinePricing.Compute(facts, maxChargeable);
        var availability = Availability(variant, stock, gross, yieldBp, lowStockThreshold: null);

        if (line.Quantity > variant.MaxOrder)
        {
            issues.Add(new CartIssueView(CartErrors.QuantityAboveMax, $"You can order at most {FormatQuantity(variant, variant.MaxOrder)} of this item; reduce the quantity."));
        }

        switch (availability.Status)
        {
            case LineAvailability.NotSoldHere:
                issues.Add(new CartIssueView(CartErrors.StockItemMissing, "This item is not stocked at your location; remove it from your cart."));
                break;
            case LineAvailability.OutOfStock:
                issues.Add(new CartIssueView(CartErrors.OutOfStock, availability.Message ?? "Out of stock today."));
                break;
        }

        return new PricedLine(line, variant, resolved.Preparation, price, gross, maxChargeable, estimated, max, availability, issues);
    }

    public static LinePriceFacts FactsOf(VariantInfo variant, LinePrice price)
    {
        ArgumentNullException.ThrowIfNull(variant);
        ArgumentNullException.ThrowIfNull(price);
        return new LinePriceFacts(
            variant.StockUnit == StockUnit.Gram ? PricedUnit.Gram : PricedUnit.Piece,
            price.UnitPricePaise,
            price.PreparationChargePaise,
            price.ChargeBasis switch
            {
                ChargeBasis.PerKg => PreparationChargeBasis.PerKg,
                ChargeBasis.PerItem => PreparationChargeBasis.PerItem,
                ChargeBasis.Flat => PreparationChargeBasis.Flat,
                _ => null,
            },
            price.TaxRateBp);
    }

    /// <summary>Yield of the preparation; PIECE stock ignores yield (D10, INV-R11).</summary>
    public static int YieldOf(VariantInfo variant, PreparationInfo? preparation)
    {
        ArgumentNullException.ThrowIfNull(variant);
        return variant.StockUnit == StockUnit.Gram && preparation is { YieldBp: > 0 } ? preparation.YieldBp : BasisPoints.Whole;
    }

    /// <summary>Gross stock need (CART-R9): GRAM ceil(net × 10000 / yield); PIECE the count.</summary>
    public static int GrossQuantity(StockUnit stockUnit, int netQuantity, int yieldBp)
        => stockUnit == StockUnit.Gram ? Weight.GrossForNet(netQuantity, yieldBp) : netQuantity;

    /// <summary>
    /// Availability status and message (CART-R13, R14): NOT_SOLD_HERE without a stock item, OUT_OF_STOCK when
    /// available &lt; gross need, LOW_STOCK when available ≤ threshold, else IN_STOCK.
    /// </summary>
    public static AvailabilityResult Availability(VariantInfo variant, VariantAvailability? stock, int grossNeed, int yieldBp, int? lowStockThreshold)
    {
        ArgumentNullException.ThrowIfNull(variant);
        if (stock is null)
        {
            return new AvailabilityResult(LineAvailability.NotSoldHere, "Not sold at your location.");
        }

        var available = Math.Max(0, stock.AvailableQuantity);
        var availableNet = AvailableNet(variant, available, yieldBp);
        if (available < grossNeed)
        {
            return availableNet >= variant.MinOrder && availableNet > 0
                ? new AvailabilityResult(LineAvailability.OutOfStock, $"Only {FormatQuantity(variant, availableNet)} available; reduce the quantity.")
                : new AvailabilityResult(LineAvailability.OutOfStock, "Out of stock today.");
        }

        return lowStockThreshold is { } threshold && available <= threshold
            ? new AvailabilityResult(LineAvailability.LowStock, $"Only {FormatQuantity(variant, availableNet)} left today.")
            : new AvailabilityResult(LineAvailability.InStock, null);
    }

    /// <summary>Net quantity orderable from available stock: GRAM floor(available × yield / 10000) down to the step.</summary>
    public static int AvailableNet(VariantInfo variant, int available, int yieldBp)
    {
        ArgumentNullException.ThrowIfNull(variant);
        if (variant.StockUnit != StockUnit.Gram)
        {
            return available;
        }

        var net = (int)((long)available * yieldBp / BasisPoints.Whole);
        return variant.OrderStep > 0 ? net / variant.OrderStep * variant.OrderStep : net;
    }

    /// <summary>The API quantity unit of a sell unit: KG → GRAM, PIECE → PIECE, PACK → PACK.</summary>
    public static string QuantityUnitCode(SellUnit sellUnit) => sellUnit switch
    {
        SellUnit.Kg => "GRAM",
        SellUnit.Pack => "PACK",
        _ => "PIECE",
    };

    public static string FormatQuantity(VariantInfo variant, int quantity)
    {
        ArgumentNullException.ThrowIfNull(variant);
        return variant.SellUnit switch
        {
            SellUnit.Kg when quantity >= Weight.GramsPerKg => $"{(quantity / 1000m).ToString("0.###", CultureInfo.InvariantCulture)} kg",
            SellUnit.Kg => $"{quantity.ToString(CultureInfo.InvariantCulture)} g",
            SellUnit.Pack => quantity == 1 ? "1 pack" : $"{quantity.ToString(CultureInfo.InvariantCulture)} packs",
            _ => quantity == 1 ? "1 piece" : $"{quantity.ToString(CultureInfo.InvariantCulture)} pieces",
        };
    }

    private static PricedLine Unpriced(ResolvedLine resolved, List<CartIssueView> issues)
        => new(
            resolved.Line,
            resolved.Variant,
            resolved.Preparation,
            null,
            0,
            resolved.Line.Quantity,
            default,
            default,
            new AvailabilityResult(LineAvailability.NotSoldHere, "Not sold at your location."),
            issues);
}
