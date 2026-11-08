using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Identity.Application.Shared;

/// <summary>Configuration section <c>Identity</c>; defaults are the documented values (docs/api §1.2, §1.9).</summary>
public sealed class IdentitySettings
{
    public const string SectionName = "Identity";

    public int OtpLifetimeMinutes { get; set; } = 5;

    public int OtpResendSeconds { get; set; } = 30;

    public int OtpMaxAttempts { get; set; } = 5;

    public int OtpPerPhonePer10Minutes { get; set; } = 3;

    public int OtpPerPhonePerDay { get; set; } = 10;

    /// <summary>
    /// IDN-R4: codes per client IP per hour. High because many customers share one carrier NAT address (ME-8 / MB-5);
    /// <see cref="OtpPerPhonePer10Minutes"/> and <see cref="OtpPerPhonePerDay"/> are the main guard.
    /// </summary>
    public int OtpPerIpPerHour { get; set; } = 300;

    public int CustomerRefreshTokenDays { get; set; } = 30;

    public int StaffRefreshTokenHours { get; set; } = 12;

    /// <summary>D33: consecutive wrong passwords that lock a staff account.</summary>
    public int StaffMaxFailedLogins { get; set; } = 10;

    /// <summary>D33: length of the first lock; every further lock since the last sign-in doubles it.</summary>
    public int StaffLockoutMinutes { get; set; } = 15;

    /// <summary>D33: the longest lock.</summary>
    public int StaffMaxLockoutHours { get; set; } = 24;

    /// <summary>IDN-R20 (docs/api §1.9 <c>staff-login</c>): failed logins allowed per e-mail + client IP in the window.</summary>
    public int StaffLoginFailuresPerEmailIp { get; set; } = 5;

    public int StaffLoginFailureWindowMinutes { get; set; } = 15;

    public const int OtpCodeLength = 6;

    public LockoutPolicy StaffLockoutPolicy() => new(StaffMaxFailedLogins, TimeSpan.FromMinutes(StaffLockoutMinutes), TimeSpan.FromHours(StaffMaxLockoutHours));
}

/// <summary>Keyed hash of an OTP code (IDN-R1).</summary>
public interface IOtpCodeHasher
{
    string Hash(Guid challengeId, string code);
}

/// <summary>Password hashing (ASP.NET Core PasswordHasher in Infrastructure).</summary>
public interface IPasswordHashing
{
    string Hash(string password);

    /// <summary>True when <paramref name="password"/> matches; <paramref name="needsRehash"/> when the hash should be upgraded.</summary>
    bool Verify(string hash, string password, out bool needsRehash);

    /// <summary>Spends the time of one <see cref="Verify"/> against a hash no account has, so refusals without a real hash are not faster (D33).</summary>
    void VerifyDummy(string password);
}

public sealed record AccessTokenClaims(
    Guid UserId,
    bool IsStaff,
    Guid? CustomerId,
    Guid? StaffMemberId,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<Guid> LocationIds,
    bool AllLocations);

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Signs JWT access tokens (SharedKernel.Web <c>AccessTokenFactory</c> in Infrastructure).</summary>
public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(AccessTokenClaims claims);
}

/// <summary>Facts about the current HTTP request.</summary>
public interface IRequestContext
{
    string? ClientIp { get; }
}

/// <summary>Opaque refresh tokens: <c>rt_</c> + 32 random bytes (base64url); stored as SHA-256 hex.</summary>
public static class OpaqueTokens
{
    public static string NewRefreshToken() => "rt_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>A uniformly random numeric code of <paramref name="length"/> digits.</summary>
    public static string NewNumericCode(int length)
    {
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            builder.Append((char)('0' + RandomNumberGenerator.GetInt32(10)));
        }

        return builder.ToString();
    }
}

/// <summary>IDN-R13: minimum length 12 and not a well-known password.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;

    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456789012", "1234567890123", "password1234", "password12345", "passwordpassword", "qwertyuiopas", "qwerty123456",
        "111111111111", "000000000000", "abcdefghijkl", "iloveyou1234", "welcome12345", "admin1234567", "administrator",
        "letmein12345", "changeme1234", "freshorbit12", "freshorbit123", "p@ssw0rd1234", "Password@123",
    };

    public static void Ensure(string? password, string field)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength || Common.Contains(password) || password.Distinct().Count() < 4)
        {
            throw new DomainException(IdentityErrors.PasswordTooWeak, $"The {field} must have at least {MinLength} characters and must not be a common password.", field);
        }
    }
}

public static partial class PhoneNumbers
{
    /// <summary>E.164: '+' then 8 to 15 digits, the first not zero.</summary>
    public static bool IsE164(string? phone) => phone is not null && E164().IsMatch(phone);

    [GeneratedRegex(@"^\+[1-9][0-9]{7,14}$")]
    private static partial Regex E164();
}
