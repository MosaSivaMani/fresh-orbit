using System.Security.Cryptography;
using System.Text;
using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Identity.Domain.Users;

/// <summary>
/// One refused staff login (IDN-R20): the per email + client IP limit counts these rows. Written for unknown e-mails
/// too, so the 429 answer does not reveal whether an account exists. Only a SHA-256 of the normalised e-mail is kept.
/// Append-only, never updated.
/// </summary>
public sealed class StaffLoginFailure : Entity
{
    private StaffLoginFailure()
    {
        EmailHash = ClientIp = string.Empty;
    }

    public string EmailHash { get; private set; }

    /// <summary>The client IP as the API saw it, or <see cref="UnknownIp"/>.</summary>
    public string ClientIp { get; private set; }

    public DateTimeOffset FailedAt { get; private set; }

    public const string UnknownIp = "unknown";

    public static StaffLoginFailure Record(string normalizedEmail, string? clientIp, DateTimeOffset now)
        => new() { EmailHash = HashEmail(normalizedEmail), ClientIp = KeyOf(clientIp), FailedAt = now };

    /// <summary>Lowercase hex SHA-256 of the normalised e-mail.</summary>
    public static string HashEmail(string normalizedEmail)
    {
        ArgumentNullException.ThrowIfNull(normalizedEmail);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)));
    }

    public static string KeyOf(string? clientIp) => string.IsNullOrWhiteSpace(clientIp) ? UnknownIp : clientIp;
}
