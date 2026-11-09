using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Reporting.Domain.Buying;

/// <summary>
/// "Today's prices" figures (D370, REP-R15): what a sell unit costs from the latest batch, the margin of the current
/// price and a suggested price for a target margin. Money in paise, rates in basis points. Suggestions are shown,
/// never applied (staff save prices through the Pricing bulk endpoint).
/// </summary>
public static class PriceRules
{
    public const string Kg = "KG";

    /// <summary>Highest target margin a suggestion accepts (95 %); 100 % would divide by zero.</summary>
    public const int MaxTargetMarginBp = 9_500;

    /// <summary>Suggested prices are rounded up to whole rupees.</summary>
    public const long SuggestionStepPaise = Money.PaisePerRupee;

    /// <summary>
    /// Cost of one sell unit, net, from a batch. KG (stock in raw grams): per net kg =
    /// total cost × 1000 × 10 000 / (received grams × yield), half away from zero, because cleaning loses
    /// (10 000 − yield) of every raw kg. PIECE and PACK (stock counted in pieces or packs): total cost / received count.
    /// Null when a KG variant has no yield (rule 7: never guessed) or nothing was received.
    /// </summary>
    public static long? CostPerSellUnitPaise(string sellUnit, long batchCostPaise, int receivedQuantity, int? yieldBp)
    {
        if (receivedQuantity <= 0)
        {
            return null;
        }

        if (sellUnit != Kg)
        {
            return Rounding.DivideHalfAwayFromZero(batchCostPaise, receivedQuantity);
        }

        if (yieldBp is not (> 0 and <= BasisPoints.Whole))
        {
            return null;
        }

        return Rounding.DivideHalfAwayFromZero(
            (Int128)batchCostPaise * Weight.GramsPerKg * BasisPoints.Whole,
            (long)receivedQuantity * yieldBp.Value);
    }

    /// <summary>A tax-inclusive price without its GST (PRC-R10): price × 10 000 / (10 000 + rate), half away from zero.</summary>
    public static long PriceExTaxPaise(long pricePaise, int taxRateBp)
        => Rounding.DivideHalfAwayFromZero((Int128)pricePaise * BasisPoints.Whole, BasisPoints.Whole + (long)taxRateBp);

    /// <summary>
    /// Margin of a price over a cost, both without tax: (price − cost) × 10 000 / price, truncated toward zero as the
    /// contribution report does (REP-R5); null when the price is not positive.
    /// </summary>
    public static int? MarginBp(long priceExTaxPaise, long costPaise)
    {
        if (priceExTaxPaise <= 0)
        {
            return null;
        }

        var margin = (Int128)(priceExTaxPaise - costPaise) * BasisPoints.Whole / priceExTaxPaise;
        return (int)Int128.Clamp(margin, int.MinValue, int.MaxValue);
    }

    /// <summary>
    /// Tax-inclusive price that earns <paramref name="targetMarginBp"/> on <paramref name="costPaise"/>:
    /// ex-tax = cost ÷ (1 − target), then GST added, then rounded up to whole rupees. Each step rounds up so the
    /// suggestion never falls below the target.
    /// </summary>
    public static long SuggestedPricePaise(long costPaise, int targetMarginBp, int taxRateBp)
    {
        if (targetMarginBp is < 0 or > MaxTargetMarginBp)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMarginBp), targetMarginBp, "Target margin must be between 0 and 9500 bp.");
        }

        var exTax = Rounding.DivideCeiling((Int128)costPaise * BasisPoints.Whole, BasisPoints.Whole - targetMarginBp);
        var withTax = Rounding.DivideCeiling((Int128)exTax * (BasisPoints.Whole + (long)taxRateBp), BasisPoints.Whole);
        return Rounding.DivideCeiling(withTax, SuggestionStepPaise) * SuggestionStepPaise;
    }
}
