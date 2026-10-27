using System.Text.RegularExpressions;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Locations.Domain.Shared;

/// <summary>Format rules shared by the Locations aggregates.</summary>
public static partial class Rules
{
    public const int NameMaxLength = 120;
    public const int CodeMaxLength = 40;

    /// <summary>Longest state display name (city <c>state_name</c>).</summary>
    public const int StateNameMaxLength = 60;

    /// <summary>The trimmed text, or null when it is null or blank.</summary>
    public static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>LOC-R2: six digits, not starting with 0.</summary>
    public static bool IsPincode(string? value) => value is not null && PincodePattern().IsMatch(value);

    /// <summary>LOC-R10: UPPER_SNAKE city code.</summary>
    public static bool IsCityCode(string? value) => value is { Length: <= CodeMaxLength } && ErrorCodes.IsUpperSnake(value);

    /// <summary>LOC-R15: upper-case letters and digits in groups joined by <c>_</c> or <c>-</c> (<c>CHN-TVM</c>).</summary>
    public static bool IsLocationCode(string? value) => value is { Length: <= CodeMaxLength } && LocationCodePattern().IsMatch(value);

    /// <summary>Two-letter Indian state / union territory code (TN, KA, DL…).</summary>
    public static bool IsStateCode(string? value) => value is not null && StateCodePattern().IsMatch(value);

    public static bool IsTimeZone(string? value) => !string.IsNullOrWhiteSpace(value) && TimeZoneInfo.TryFindSystemTimeZoneById(value, out _);

    /// <summary>Today's date in <paramref name="timeZone"/> (LOC-R17).</summary>
    public static DateOnly Today(DateTimeOffset utcNow, string timeZone)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);

    public static void RequireName(List<FieldError> errors, string field, string? value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > NameMaxLength)
        {
            errors.Add(new FieldError(field, LocationsErrors.ValidationFailed, $"Enter a {field} of 1 to {NameMaxLength} characters."));
        }
    }

    public static void ThrowIfAny(List<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }
    }

    [GeneratedRegex("^[1-9][0-9]{5}$")]
    private static partial Regex PincodePattern();

    [GeneratedRegex("^[A-Z0-9]+([_-][A-Z0-9]+)*$")]
    private static partial Regex LocationCodePattern();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex StateCodePattern();
}
