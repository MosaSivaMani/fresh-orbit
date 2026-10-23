using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.SharedKernel.Pricing;

/// <summary>
/// How a line's quantity is counted for pricing. <see cref="Gram"/>: quantity in grams, unit price per kg.
/// <see cref="Piece"/>: quantity in pieces or packs, unit price per piece or pack.
/// </summary>
public enum PricedUnit
{
    Gram,
    Piece,
}

/// <summary>Basis of a preparation charge. Stored as PER_KG / PER_ITEM / FLAT.</summary>
public enum PreparationChargeBasis
{
    PerKg,
    PerItem,
    Flat,
}

/// <summary>
/// The price facts of one line (from the current price list for a cart or checkout, from the order item snapshot
/// at pack time).
/// </summary>
/// <param name="UnitPricePaise">Tax-inclusive price per kg (GRAM) or per piece / pack (PIECE).</param>
/// <param name="PreparationChargePaise">Charge rate in the unit of <paramref name="ChargeBasis"/>; 0 when there is none.</param>
/// <param name="ChargeBasis">Basis of the preparation charge; null when there is no charge.</param>
/// <param name="TaxRateBp">GST rate contained in the price, in basis points.</param>
public sealed record LinePriceFacts(
    PricedUnit Unit,
    long UnitPricePaise,
    long PreparationChargePaise,
    PreparationChargeBasis? ChargeBasis,
    int TaxRateBp);

/// <summary>Amounts of one line at one quantity. <see cref="TotalPaise"/> = item + preparation.</summary>
public readonly record struct LineAmounts(long ItemPaise, long PreparationPaise)
{
    public long TotalPaise => checked(ItemPaise + PreparationPaise);
}

/// <summary>
/// The one line formula shared by Cart (priced on read), Checkout (order snapshot) and weigh-and-pack (final
/// amounts) so the three can never diverge (decision D20). Rounding is per line, half up (half away from zero;
/// amounts here are never negative), never on totals.
/// <list type="bullet">
/// <item>Item: GRAM = round(quantity × unitPrice / 1000); PIECE = quantity × unitPrice (CART-R3).</item>
/// <item>Preparation: PER_KG = round(rate × grams / 1000) (GRAM lines only); PER_ITEM = rate × count (PIECE lines
/// only); FLAT = rate once per line (CART-R4).</item>
/// <item>Tax is contained in the tax-inclusive amount: round(amount × rate / (10000 + rate)) (CART-R6).</item>
/// <item>Max chargeable quantity: weight-variable = floor(quantity × (10000 + maxOverweightBp) / 10000), else the
/// quantity (CART-R7, ORD-R18).</item>
/// </list>
/// </summary>
public static class LinePricing
{
    /// <summary>
    /// True when the preparation charge basis can be applied to the unit: PER_KG needs grams, PER_ITEM needs a
    /// count. A line with an unsupported combination is a configuration problem (PREPARATION_CHARGE_NOT_AVAILABLE).
    /// </summary>
    public static bool IsSupported(PricedUnit unit, PreparationChargeBasis? basis) => basis switch
    {
        null or PreparationChargeBasis.Flat => true,
        PreparationChargeBasis.PerKg => unit == PricedUnit.Gram,
        PreparationChargeBasis.PerItem => unit == PricedUnit.Piece,
        _ => false,
    };

    /// <summary>Item and preparation amounts of a line at <paramref name="quantity"/> (grams or pieces).</summary>
    /// <exception cref="ArgumentException">The charge basis does not fit the unit (see <see cref="IsSupported"/>).</exception>
    public static LineAmounts Compute(LinePriceFacts facts, int quantity)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(facts.UnitPricePaise);
        ArgumentOutOfRangeException.ThrowIfNegative(facts.PreparationChargePaise);
        if (!IsSupported(facts.Unit, facts.ChargeBasis))
        {
            throw new ArgumentException($"A {facts.ChargeBasis} preparation charge cannot be applied to a {facts.Unit} line.", nameof(facts));
        }

        var item = facts.Unit == PricedUnit.Gram
            ? Money.ForGrams(facts.UnitPricePaise, quantity)
            : Money.Multiply(facts.UnitPricePaise, quantity);
        return new LineAmounts(item, PreparationCharge(facts, quantity));
    }

    /// <summary>
    /// The tax contained in a tax-inclusive amount: round(amount × rate / (10000 + rate)). 10 500 paise at 500 bp
    /// contains 500 paise.
    /// </summary>
    public static long ContainedTax(long amountPaise, int taxRateBp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(taxRateBp);
        return taxRateBp == 0 ? 0 : Rounding.DivideHalfAwayFromZero((Int128)amountPaise * taxRateBp, BasisPoints.Whole + (long)taxRateBp);
    }

    /// <summary>
    /// The most that may be charged for an ordered quantity: weight-variable lines floor(quantity × (10000 +
    /// maxOverweightBp) / 10000) (333 g at 1000 bp → 366 g); fixed-weight lines the ordered quantity.
    /// </summary>
    public static int MaxChargeableQuantity(int orderedQuantity, bool isWeightVariable, int maxOverweightBp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(orderedQuantity);
        ArgumentOutOfRangeException.ThrowIfNegative(maxOverweightBp);
        return isWeightVariable
            ? checked((int)((long)orderedQuantity * (BasisPoints.Whole + (long)maxOverweightBp) / BasisPoints.Whole))
            : orderedQuantity;
    }

    /// <summary>The quantity charged at pack: the packed quantity, capped at the max chargeable quantity.</summary>
    public static int ChargedQuantity(int packedQuantity, int maxChargeableQuantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(packedQuantity);
        return Math.Min(packedQuantity, maxChargeableQuantity);
    }

    private static long PreparationCharge(LinePriceFacts facts, int quantity) => facts.ChargeBasis switch
    {
        null => 0,
        PreparationChargeBasis.PerKg => Money.ForGrams(facts.PreparationChargePaise, quantity),
        PreparationChargeBasis.PerItem => Money.Multiply(facts.PreparationChargePaise, quantity),
        PreparationChargeBasis.Flat => quantity == 0 ? 0 : facts.PreparationChargePaise,
        _ => throw new ArgumentOutOfRangeException(nameof(facts), facts.ChargeBasis, "Unknown preparation charge basis."),
    };
}
