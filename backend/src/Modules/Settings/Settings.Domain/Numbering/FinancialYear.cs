using System.Globalization;

namespace FreshOrbit.Settings.Domain.Numbering;

/// <summary>The Indian financial year (1 April – 31 March), labelled <c>2026-27</c>.</summary>
public static class FinancialYear
{
    /// <summary>The label of the financial year containing <paramref name="localDate"/>.</summary>
    public static string LabelFor(DateOnly localDate)
    {
        var startYear = localDate.Month >= 4 ? localDate.Year : localDate.Year - 1;
        return string.Create(CultureInfo.InvariantCulture, $"{startYear:D4}-{(startYear + 1) % 100:D2}");
    }

    /// <summary>The label for an instant seen in <paramref name="timeZone"/>.</summary>
    public static string LabelFor(DateTimeOffset at, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        return LabelFor(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, timeZone).DateTime));
    }

    public static bool IsValidLabel(string? label)
    {
        if (label is null || label.Length != 7 || label[4] != '-')
        {
            return false;
        }

        if (!int.TryParse(label.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(label.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var end))
        {
            return false;
        }

        return start is >= 2000 and <= 2999 && (start + 1) % 100 == end;
    }
}
