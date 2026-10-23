using System.Globalization;
using System.Text.RegularExpressions;
using FreshOrbit.Settings.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Settings.Domain.Numbering;

/// <summary>A gapless counter for one code (ORDER, INVOICE) and financial-year period. Rules SET-R4–R6.</summary>
public sealed partial class NumberSeries : AggregateRoot
{
    public static readonly IReadOnlyList<string> Codes = ["ORDER", "INVOICE"];

    private NumberSeries()
    {
        Code = Period = Prefix = string.Empty;
    }

    public string Code { get; private set; }

    /// <summary>Financial-year label, e.g. 2026-27.</summary>
    public string Period { get; private set; }

    public string Prefix { get; private set; }

    public int PadWidth { get; private set; }

    public long NextValue { get; private set; }

    /// <summary>When the last number was drawn; null while no number was issued.</summary>
    public DateTimeOffset? LastIssuedAt { get; private set; }

    public bool IsInUse => LastIssuedAt is not null;

    public static NumberSeries Create(string code, string period, string prefix, int padWidth, long nextValue)
    {
        EnsureKey(code, period);
        var series = new NumberSeries { Code = code, Period = period };
        series.Apply(prefix, padWidth, nextValue);
        return series;
    }

    /// <summary>Changes the format; refused once a number has been issued.</summary>
    public void Change(string prefix, int padWidth, long nextValue)
    {
        if (IsInUse)
        {
            throw new ConflictException(SettingsErrors.NumberSeriesInUse, "This number series already issued numbers; its prefix and next value can no longer change.");
        }

        Apply(prefix, padWidth, nextValue);
    }

    /// <summary>Draws the next number and advances the counter. The caller must hold the row lock.</summary>
    public string Next(DateTimeOffset at)
    {
        var number = Format(NextValue);
        NextValue = checked(NextValue + 1);
        LastIssuedAt = at;
        return number;
    }

    public string Format(long value) => Prefix + value.ToString(CultureInfo.InvariantCulture).PadLeft(PadWidth, '0');

    public static void EnsureKey(string code, string period)
    {
        var errors = new List<FieldError>();
        if (!Codes.Contains(code))
        {
            errors.Add(new FieldError("code", "OUT_OF_RANGE", "The code must be ORDER or INVOICE."));
        }

        if (!FinancialYear.IsValidLabel(period))
        {
            errors.Add(new FieldError("period", "PERIOD_INVALID", "The period must be a financial year such as 2026-27."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }
    }

    private void Apply(string prefix, int padWidth, long nextValue)
    {
        var errors = new List<FieldError>();
        if (string.IsNullOrEmpty(prefix) || prefix.Length > 20 || !PrefixPattern().IsMatch(prefix))
        {
            errors.Add(new FieldError("prefix", "PREFIX_INVALID", "The prefix must be 1 to 20 letters, digits, '-' or '/'."));
        }

        if (padWidth is < 1 or > 10)
        {
            errors.Add(new FieldError("padWidth", "OUT_OF_RANGE", "The padWidth must be between 1 and 10."));
        }

        if (nextValue < 1)
        {
            errors.Add(new FieldError("nextValue", "OUT_OF_RANGE", "The nextValue must be 1 or more."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        Prefix = prefix;
        PadWidth = padWidth;
        NextValue = nextValue;
    }

    [GeneratedRegex("^[A-Z0-9/-]+$")]
    private static partial Regex PrefixPattern();
}
