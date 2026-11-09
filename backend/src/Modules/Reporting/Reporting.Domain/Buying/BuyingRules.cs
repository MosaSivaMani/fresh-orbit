using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Reporting.Domain.Buying;

/// <summary>
/// The morning buying list (D369, REP-R14): what the confirmed orders of one delivery date need from the harbour.
/// Quantities are in the variant's stock unit (GRAM raw grams, PIECE pieces or packs); grams are <see cref="int"/>
/// per line and <see cref="long"/> once summed.
/// </summary>
public static class BuyingRules
{
    public const string Gram = "GRAM";
    public const string Pack = "PACK";

    /// <summary>The safety margin a buyer may add on the screen: 0 to 100 %.</summary>
    public const int MaxSafetyMarginBp = BasisPoints.Whole;

    /// <summary>
    /// Stock one order line needs (INV-R11, the same quantity checkout reserved): GRAM lines need the gross raw grams of
    /// the ordered net grams at the line's yield, rounded up; PIECE and PACK lines need the ordered count.
    /// </summary>
    public static int NeededQuantity(string stockUnit, int orderedQuantity, int yieldBp)
        => stockUnit == Gram ? Weight.GrossForNet(orderedQuantity, yieldBp) : orderedQuantity;

    /// <summary>
    /// Net grams the customer ordered: the ordered grams of a GRAM line; packs × pack net grams of a PACK line (D348);
    /// null for pieces, whose weight is only a display average (rule 7: never guessed).
    /// </summary>
    public static int? OrderedNetGrams(string stockUnit, string sellUnit, int orderedQuantity, int? packSizeGrams)
    {
        if (stockUnit == Gram)
        {
            return orderedQuantity;
        }

        return sellUnit == Pack && packSizeGrams is > 0 ? checked(orderedQuantity * packSizeGrams.Value) : null;
    }

    /// <summary>Estimated raw (uncleaned) grams to buy for a line: its net grams at its yield, rounded up; null without net grams.</summary>
    public static int? EstimatedRawGrams(int? orderedNetGrams, int yieldBp)
        => orderedNetGrams is { } net ? Weight.GrossForNet(net, yieldBp) : null;

    /// <summary><paramref name="quantity"/> plus the safety margin, rounded up so the margin never buys less.</summary>
    public static long WithMargin(long quantity, int safetyMarginBp)
        => Rounding.DivideCeiling((Int128)quantity * (BasisPoints.Whole + (long)safetyMarginBp), BasisPoints.Whole);

    /// <summary>
    /// Stock that can serve the listed orders: on hand minus what other orders (other dates, or packed orders not yet
    /// consumed) hold reserved. Never below zero. No stock item = nothing on hand.
    /// </summary>
    public static long Available(long? onHand, long reserved, long reservedForListedOrders)
        => onHand is { } hand ? Math.Max(0, hand - Math.Max(0, reserved - reservedForListedOrders)) : 0;

    /// <summary>What still has to be bought: the need with margin minus the available stock, never below zero.</summary>
    public static long Shortfall(long withMarginQuantity, long available) => Math.Max(0, withMarginQuantity - Math.Max(0, available));

    /// <summary>
    /// The shortfall in raw grams: the shortfall itself for GRAM stock; for packs, the shortfall's share of the raw
    /// grams with margin (packs × average raw grams per pack, rounded up); null when raw grams are unknown.
    /// </summary>
    public static long? ShortfallRawGrams(string stockUnit, long shortfall, long withMarginQuantity, long? withMarginRawGrams)
    {
        if (stockUnit == Gram)
        {
            return shortfall;
        }

        if (withMarginRawGrams is not { } raw || withMarginQuantity <= 0)
        {
            return null;
        }

        return Rounding.DivideCeiling((Int128)shortfall * raw, withMarginQuantity);
    }
}
