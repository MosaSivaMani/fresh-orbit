using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace FreshOrbit.Catalog.Domain.Shared;

/// <summary>CAT-R1: lowercase kebab slugs of at most 80 characters.</summary>
public static partial class Slugs
{
    public const int MaxLength = 80;

    public static bool IsValid(string? slug) => slug is { Length: > 0 and <= MaxLength } && SlugPattern().IsMatch(slug);

    /// <summary>A slug made from a name: ASCII letters and digits, other runs become one hyphen. Empty when nothing is left.</summary>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Slugs are lowercase by definition (CAT-R1).")]
    public static string FromName(string? name)
    {
        var builder = new StringBuilder();
        foreach (var c in (name ?? string.Empty).ToLowerInvariant())
        {
            if (char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length <= MaxLength ? slug : slug[..MaxLength].TrimEnd('-');
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
