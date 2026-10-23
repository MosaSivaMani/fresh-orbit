namespace FreshOrbit.SharedKernel.Web.RateLimiting;

/// <summary>
/// Rate limiter policy names registered by the Api host (docs/api §1.9); use with <c>.RequireRateLimiting(...)</c>.
/// Per-IP policies use the client IP after forwarded-headers processing (only trusted proxies are believed).
/// </summary>
public static class RateLimitPolicies
{
    /// <summary><c>POST /auth/otp/request</c>: per client IP (per-phone limits are enforced by Identity).</summary>
    public const string OtpRequest = "otp-request";

    /// <summary><c>POST /auth/otp/verify</c>: per client IP (per-challenge attempts are enforced by Identity).</summary>
    public const string OtpVerify = "otp-verify";

    /// <summary>Staff password login: per client IP.</summary>
    public const string Login = "staff-login";

    /// <summary><c>POST /auth/refresh</c>: per client IP.</summary>
    public const string Refresh = "refresh";

    /// <summary>Checkout / place order / payment initiate: per user (IP when anonymous).</summary>
    public const string Checkout = "checkout";

    /// <summary>Product list and search (<c>GET /products</c>, <c>/search/*</c>): per client IP.</summary>
    public const string Search = "search";

    /// <summary>Other anonymous storefront and location endpoints: per client IP.</summary>
    public const string Public = "public";

    /// <summary>Applying a coupon to the cart: per user (IP when anonymous), against code guessing.</summary>
    public const string Coupon = "coupon";
}
