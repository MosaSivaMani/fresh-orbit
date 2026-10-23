namespace FreshOrbit.SharedKernel.Storage;

/// <summary>
/// Rules for storage keys: 1–512 characters; segments separated by single <c>/</c>; each segment uses only
/// <c>a-z 0-9 - _ .</c>, does not start with <c>.</c> and is not empty. This keeps keys safe as file paths and URLs.
/// </summary>
public static class StorageKeys
{
    public const int MaxLength = 512;

    public static bool IsValid(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxLength)
        {
            return false;
        }

        foreach (var segment in key.Split('/'))
        {
            if (segment.Length == 0 || segment[0] == '.')
            {
                return false;
            }

            foreach (var c in segment)
            {
                if (!(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '_' or '.'))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Throws <see cref="ArgumentException"/> when <paramref name="key"/> breaks the key rules.</summary>
    public static string EnsureValid(string key)
        => IsValid(key) ? key : throw new ArgumentException($"'{key}' is not a valid storage key.", nameof(key));
}
