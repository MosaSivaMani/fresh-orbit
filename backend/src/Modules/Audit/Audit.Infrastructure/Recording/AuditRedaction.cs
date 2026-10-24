using System.Text.RegularExpressions;

namespace FreshOrbit.Audit.Infrastructure.Recording;

/// <summary>
/// Which property values never reach <c>audit.entries</c> (AUD-R7). A property whose PascalCase name contains one of
/// the sensitive words is stored as <see cref="Placeholder"/>; that the value changed is still recorded.
/// </summary>
public static partial class AuditRedaction
{
    public const string Placeholder = "[REDACTED]";

    /// <summary>Password and token hashes, OTP codes, secrets, keys and raw provider payloads (which hold PII).</summary>
    public static bool IsSensitive(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        return SensitiveName().IsMatch(propertyName);
    }

    [GeneratedRegex("(Hash|Password|Secret|Token|Otp|Salt|ApiKey|PrivateKey|SigningKey|Cvv|Payload)", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveName();
}
