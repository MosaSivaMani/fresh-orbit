namespace FreshOrbit.Settings.Domain.BusinessProfiles;

/// <summary>
/// GSTIN check: 2-digit state code (01–38 or 97/99), 10-character PAN (5 letters, 4 digits, 1 letter), entity number
/// (1–9 or A–Z), the letter Z, and a base-36 check character (Luhn mod 36 over the first 14 characters).
/// </summary>
public static class Gstins
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static bool IsValid(string? gstin)
    {
        if (gstin is null || gstin.Length != 15)
        {
            return false;
        }

        if (!char.IsAsciiDigit(gstin[0]) || !char.IsAsciiDigit(gstin[1]))
        {
            return false;
        }

        var state = ((gstin[0] - '0') * 10) + (gstin[1] - '0');
        if (state is < 1 or > 38 && state is not 97 and not 99)
        {
            return false;
        }

        for (var i = 2; i < 7; i++)
        {
            if (!char.IsAsciiLetterUpper(gstin[i]))
            {
                return false;
            }
        }

        for (var i = 7; i < 11; i++)
        {
            if (!char.IsAsciiDigit(gstin[i]))
            {
                return false;
            }
        }

        if (!char.IsAsciiLetterUpper(gstin[11]) || gstin[12] == '0' || Alphabet.IndexOf(gstin[12], StringComparison.Ordinal) < 0 || gstin[13] != 'Z')
        {
            return false;
        }

        return CheckCharacter(gstin.AsSpan(0, 14)) == gstin[14];
    }

    /// <summary>The check character for the first 14 characters of a GSTIN.</summary>
    public static char CheckCharacter(ReadOnlySpan<char> first14)
    {
        var sum = 0;
        for (var i = 0; i < first14.Length; i++)
        {
            var value = Alphabet.IndexOf(first14[i], StringComparison.Ordinal);
            if (value < 0)
            {
                return '?';
            }

            var product = value * ((i % 2) + 1);
            sum += (product / 36) + (product % 36);
        }

        return Alphabet[(36 - (sum % 36)) % 36];
    }
}
