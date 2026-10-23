using System.Security.Cryptography;
using System.Text;
using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Identity.Domain.Otp;

/// <summary>Stored as LOGIN / VERIFY_PHONE.</summary>
public enum OtpPurpose
{
    Login,
    VerifyPhone,
}

/// <summary>Outcome of one verification attempt (IDN-R5).</summary>
public enum OtpCheck
{
    Accepted,
    Invalid,
    Expired,
    TooManyAttempts,
}

/// <summary>A one-time password sent to a phone. Only the HMAC of the code is stored (IDN-R1).</summary>
public sealed class OtpChallenge : AggregateRoot
{
    private OtpChallenge()
    {
        Phone = CodeHash = string.Empty;
    }

    public string Phone { get; private set; }

    public OtpPurpose Purpose { get; private set; }

    public string CodeHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public string? RequestIp { get; private set; }

    /// <summary>When the challenge was requested (also the <c>created_at</c> audit column).</summary>
    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>Creates the challenge; the hash is computed by the caller over this challenge's id and the code.</summary>
    public static OtpChallenge Create(Guid id, string phone, OtpPurpose purpose, string codeHash, DateTimeOffset now, TimeSpan lifetime, string? requestIp)
        => new()
        {
            Id = id,
            Phone = phone,
            Purpose = purpose,
            CodeHash = codeHash,
            RequestedAt = now,
            ExpiresAt = now.Add(lifetime),
            RequestIp = requestIp,
        };

    /// <summary>
    /// One attempt. A wrong code increments <see cref="AttemptCount"/> (the caller must save before refusing);
    /// the attempt that reaches <paramref name="maxAttempts"/> and every later one answer <see cref="OtpCheck.TooManyAttempts"/>.
    /// </summary>
    public OtpCheck Verify(string codeHash, DateTimeOffset now, int maxAttempts)
    {
        if (ConsumedAt is not null || now >= ExpiresAt)
        {
            return OtpCheck.Expired;
        }

        if (AttemptCount >= maxAttempts)
        {
            return OtpCheck.TooManyAttempts;
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(codeHash), Encoding.ASCII.GetBytes(CodeHash)))
        {
            AttemptCount++;
            return AttemptCount >= maxAttempts ? OtpCheck.TooManyAttempts : OtpCheck.Invalid;
        }

        ConsumedAt = now;
        return OtpCheck.Accepted;
    }
}
