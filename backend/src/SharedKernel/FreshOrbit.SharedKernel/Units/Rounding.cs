namespace FreshOrbit.SharedKernel.Units;

/// <summary>
/// Integer division with the one rounding rule used for money and quantities:
/// <b>round half away from zero</b> (2.5 → 3, -2.5 → -3, 2.4 → 2). Intermediate products use 128-bit
/// integers so they cannot overflow; a result outside the <see cref="long"/> range throws.
/// </summary>
public static class Rounding
{
    public static long DivideHalfAwayFromZero(Int128 numerator, long denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException();
        }

        var (quotient, remainder) = Int128.DivRem(numerator, denominator);
        if (remainder != 0 && Int128.Abs(remainder) * 2 >= Int128.Abs(denominator))
        {
            quotient += (numerator < 0) ^ (denominator < 0) ? -1 : 1;
        }

        return checked((long)quotient);
    }

    /// <summary>Division rounded up (towards positive infinity).</summary>
    public static long DivideCeiling(Int128 numerator, long denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException();
        }

        var (quotient, remainder) = Int128.DivRem(numerator, denominator);
        if (remainder != 0 && (remainder > 0) == (denominator > 0))
        {
            quotient += 1;
        }

        return checked((long)quotient);
    }
}
