using System.Globalization;

namespace FreshOrbit.SharedKernel.Units;

/// <summary>
/// Money is a <see cref="long"/> number of paise (100 paise = 1 rupee). Helpers here are checked for overflow
/// and round half away from zero (see <see cref="Rounding"/>).
/// </summary>
public static class Money
{
    public const long PaisePerRupee = 100;

    /// <summary>Price of <paramref name="grams"/> at a per-kg price, e.g. 750 g at 60 000 paise/kg = 45 000 paise.</summary>
    public static long ForGrams(long pricePerKgPaise, int grams) => Rounding.DivideHalfAwayFromZero((Int128)pricePerKgPaise * grams, Weight.GramsPerKg);

    /// <summary>Unit price × count (pieces or packs).</summary>
    public static long Multiply(long unitPaise, int quantity) => checked(unitPaise * quantity);

    /// <summary>Sum of amounts, checked for overflow.</summary>
    public static long Sum(IEnumerable<long> amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        long total = 0;
        foreach (var amount in amounts)
        {
            total = checked(total + amount);
        }

        return total;
    }

    /// <summary>
    /// Splits <paramref name="total"/> across <paramref name="weights"/> proportionally so the parts add up exactly
    /// to the total (largest remainder; ties go to the earlier part). Used to spread an order discount over lines.
    /// </summary>
    public static IReadOnlyList<long> Allocate(long total, IReadOnlyList<long> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        if (weights.Any(w => w < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(weights), "Weights must not be negative.");
        }

        var weightSum = Sum(weights);
        var parts = new long[weights.Count];
        if (weights.Count == 0 || weightSum == 0)
        {
            return parts;
        }

        var remainders = new (Int128 Remainder, int Index)[weights.Count];
        long allocated = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            var (quotient, remainder) = Int128.DivRem((Int128)total * weights[i], weightSum);
            parts[i] = (long)quotient;
            allocated += parts[i];
            remainders[i] = (remainder, i);
        }

        var left = total - allocated;
        foreach (var (_, index) in remainders.OrderByDescending(r => r.Remainder).ThenBy(r => r.Index).Take((int)left))
        {
            parts[index] += 1;
        }

        return parts;
    }

    /// <summary>Formats paise as rupees with two decimals (invariant culture), e.g. 12 345 → "123.45".</summary>
    public static string ToRupeeString(long paise) => (paise / (decimal)PaisePerRupee).ToString("0.00", CultureInfo.InvariantCulture);
}
