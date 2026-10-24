using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.SharedKernel.Pricing;

/// <summary>
/// The saving shown next to a struck-through compare-at price (MRP). Computed by the server only, so every screen shows
/// the same number (CLAUDE.md rule 4).
/// </summary>
public static class CompareAtDiscount
{
    /// <summary>
    /// floor((compareAt − price) × 10 000 / compareAt) in basis points, or null when there is no compare-at price or it is
    /// not above the price, or the saving floors to 0 bp (no saving to show). Rounded down so the saving is never overstated: ₹1 499 → ₹1 299 is
    /// 1 334 bp (13.34 %).
    /// </summary>
    public static int? Bp(long unitPricePaise, long? compareAtPaise)
    {
        if (compareAtPaise is not { } compareAt || compareAt <= unitPricePaise || unitPricePaise < 0)
        {
            return null;
        }

        var bp = (int)((Int128)(compareAt - unitPricePaise) * BasisPoints.Whole / compareAt);
        return bp > 0 ? bp : null;
    }
}
