using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Identity.Domain.Shared;

/// <summary>Error codes of the Identity module (docs/api §2 Identity).</summary>
public static class IdentityErrors
{
    public const string OtpInvalid = "OTP_INVALID";
    public const string OtpExpired = "OTP_EXPIRED";
    public const string OtpTooManyAttempts = "OTP_TOO_MANY_ATTEMPTS";
    public const string OtpResendTooSoon = "OTP_RESEND_TOO_SOON";
    public const string RateLimited = "RATE_LIMITED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string AccountDisabled = "ACCOUNT_DISABLED";
    public const string RefreshTokenInvalid = "REFRESH_TOKEN_INVALID";
    public const string RefreshTokenReused = "REFRESH_TOKEN_REUSED";
    public const string PasswordTooWeak = "PASSWORD_TOO_WEAK";
    public const string RoleInUse = "ROLE_IN_USE";
    public const string RoleNotFound = "ROLE_NOT_FOUND";
    public const string SystemRoleReadOnly = "SYSTEM_ROLE_READ_ONLY";
    public const string LastAdmin = "LAST_ADMIN";
    public const string PermissionUnknown = "PERMISSION_UNKNOWN";
    public const string AdminAlreadyExists = "ADMIN_ALREADY_EXISTS";
    public const string Duplicate = "DUPLICATE";
    public const string ConfigurationMissing = "CONFIGURATION_MISSING";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";

    /// <summary>The ADMIN role code (holds every permission).</summary>
    public const string AdminRoleCode = "ADMIN";
}

/// <summary>A refusal the client may retry after <see cref="RetryAfterSeconds"/> (HTTP 429).</summary>
public sealed class RetryLaterException : DomainException
{
    public RetryLaterException(string code, string message, int retryAfterSeconds)
        : base(code, message)
    {
        RetryAfterSeconds = Math.Max(1, retryAfterSeconds);
    }

    public int RetryAfterSeconds { get; }
}
