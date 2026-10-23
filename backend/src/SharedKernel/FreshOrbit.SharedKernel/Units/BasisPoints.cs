namespace FreshOrbit.SharedKernel.Units;

/// <summary>
/// Rates in basis points (<c>_bp</c>): 10 000 bp = 100 %, 500 bp = 5 %.
/// Results are rounded half away from zero (see <see cref="Rounding"/>).
/// </summary>
public static class BasisPoints
{
    public const int Whole = 10_000;

    /// <summary><paramref name="amount"/> × <paramref name="bp"/> / 10 000, e.g. GST of 5 % on 10 001 paise = 500 paise.</summary>
    public static long Apply(long amount, int bp) => Rounding.DivideHalfAwayFromZero((Int128)amount * bp, Whole);

    /// <summary><paramref name="amount"/> increased by <paramref name="bp"/>: amount × (10 000 + bp) / 10 000.</summary>
    public static long Increase(long amount, int bp) => Rounding.DivideHalfAwayFromZero((Int128)amount * (Whole + (long)bp), Whole);

    /// <summary>The rate <paramref name="part"/> / <paramref name="whole"/> in basis points.</summary>
    public static int Ratio(long part, long whole) => checked((int)Rounding.DivideHalfAwayFromZero((Int128)part * Whole, whole));

    public static bool IsValidRate(int bp) => bp is >= 0 and <= Whole;
}
