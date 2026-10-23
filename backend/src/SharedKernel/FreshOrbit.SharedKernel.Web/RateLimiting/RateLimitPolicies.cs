namespace FreshOrbit.SharedKernel.Web.RateLimiting;

/// <summary>Rate limiter policy names registered by the Api host; use with <c>.RequireRateLimiting(...)</c>.</summary>
public static class RateLimitPolicies
{
    /// <summary>OTP requests: strict, per client IP.</summary>
    public const string Otp = "otp";

    /// <summary>Staff password login: strict, per client IP.</summary>
    public const string Login = "login";

    /// <summary>Checkout / place order: moderate, per user.</summary>
    public const string Checkout = "checkout";
}
