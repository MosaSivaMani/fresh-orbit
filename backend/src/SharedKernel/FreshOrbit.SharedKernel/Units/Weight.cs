namespace FreshOrbit.SharedKernel.Units;

/// <summary>Weights are <see cref="int"/> grams (<c>_grams</c>).</summary>
public static class Weight
{
    public const int GramsPerKg = 1000;

    /// <summary>
    /// Gross raw grams to reserve for a net (prepared) quantity at a yield, rounded <b>up</b> so stock is never
    /// under-reserved: ceil(net × 10 000 / yield_bp). 500 g curry cut at 6 500 bp → 770 g.
    /// </summary>
    public static int GrossForNet(int netGrams, int yieldBp)
    {
        if (yieldBp is <= 0 or > BasisPoints.Whole)
        {
            throw new ArgumentOutOfRangeException(nameof(yieldBp), yieldBp, "Yield must be between 1 and 10000 bp.");
        }

        return checked((int)Rounding.DivideCeiling((Int128)netGrams * BasisPoints.Whole, yieldBp));
    }

    /// <summary>
    /// Largest chargeable quantity for an ordered quantity with an allowed overweight, rounded half away from
    /// zero: ordered × (10 000 + max_overweight_bp) / 10 000.
    /// </summary>
    public static int MaxChargeable(int orderedQuantity, int maxOverweightBp) => checked((int)BasisPoints.Increase(orderedQuantity, maxOverweightBp));
}
