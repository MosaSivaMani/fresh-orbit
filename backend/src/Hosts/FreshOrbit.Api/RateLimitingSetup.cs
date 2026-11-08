using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Api;

/// <summary>
/// Configuration section <c>RateLimiting</c>: permits per window for each policy (docs/api §1.9). Per-IP ceilings are
/// set for many customers behind one carrier NAT address (ME-8 / MB-5); a policy with
/// <see cref="RateLimitRule.PerUserPermitLimit"/> gives each signed-in caller its own budget.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Per client IP; the per-phone limits enforced by Identity are the main guard.</summary>
    public RateLimitRule OtpRequest { get; set; } = new() { PermitLimit = 300, WindowSeconds = 3600 };

    /// <summary>Per client IP; per-challenge attempts and per-phone request limits are enforced by Identity.</summary>
    public RateLimitRule OtpVerify { get; set; } = new() { PermitLimit = 300, WindowSeconds = 600 };

    public RateLimitRule Login { get; set; } = new() { PermitLimit = 10, WindowSeconds = 300 };

    public RateLimitRule Refresh { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    public RateLimitRule Checkout { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Per signed-in user (<see cref="RateLimitRule.PerUserPermitLimit"/>), else per client IP (<see cref="RateLimitRule.PermitLimit"/>).</summary>
    public RateLimitRule Search { get; set; } = new() { PermitLimit = 600, PerUserPermitLimit = 120, WindowSeconds = 60 };

    /// <summary>Per signed-in user (<see cref="RateLimitRule.PerUserPermitLimit"/>), else per client IP (<see cref="RateLimitRule.PermitLimit"/>).</summary>
    public RateLimitRule Public { get; set; } = new() { PermitLimit = 1200, PerUserPermitLimit = 240, WindowSeconds = 60 };

    public RateLimitRule Coupon { get; set; } = new() { PermitLimit = 10, WindowSeconds = 600 };

    public RateLimitRule Upload { get; set; } = new() { PermitLimit = 20, WindowSeconds = 3600 };

    internal IEnumerable<(string Name, RateLimitRule Rule)> Rules()
    {
        yield return (nameof(OtpRequest), OtpRequest);
        yield return (nameof(OtpVerify), OtpVerify);
        yield return (nameof(Login), Login);
        yield return (nameof(Refresh), Refresh);
        yield return (nameof(Checkout), Checkout);
        yield return (nameof(Search), Search);
        yield return (nameof(Public), Public);
        yield return (nameof(Coupon), Coupon);
        yield return (nameof(Upload), Upload);
    }

    /// <summary>The rules whose policy partitions a signed-in caller by user; each needs <see cref="RateLimitRule.PerUserPermitLimit"/>.</summary>
    internal IEnumerable<(string Name, RateLimitRule Rule)> UserPartitionedRules()
    {
        yield return (nameof(Search), Search);
        yield return (nameof(Public), Public);
    }
}

public sealed class RateLimitRule
{
    /// <summary>Permits per window: per client IP (per user for the sliding per-user policies).</summary>
    public int PermitLimit { get; set; }

    /// <summary>
    /// Permits per window for one signed-in caller on a policy that partitions by user (<c>search</c>, <c>public</c>);
    /// anonymous callers share <see cref="PermitLimit"/> per client IP.
    /// </summary>
    public int? PerUserPermitLimit { get; set; }

    public int WindowSeconds { get; set; }
}

/// <summary>The partition (bucket) a request is counted in, and its permits per window.</summary>
public sealed record RateLimitBudget(string PartitionKey, int PermitLimit);

/// <summary>
/// ME-8 / MB-5: chooses the bucket for a user-or-IP policy. A signed-in caller (the <c>sub</c> claim of an authenticated
/// principal) gets its own bucket with <see cref="RateLimitRule.PerUserPermitLimit"/>, so customers sharing one carrier
/// NAT address do not use up each other's budget; anyone else is counted per client IP with
/// <see cref="RateLimitRule.PermitLimit"/>.
/// </summary>
public static class RateLimitPartitioning
{
    public static RateLimitBudget ForUserOrIp(string policy, RateLimitRule rule, ClaimsPrincipal? user, IPAddress? clientIp)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var subject = SubjectOf(user);
        if (subject is not null && rule.PerUserPermitLimit is { } perUser)
        {
            return new RateLimitBudget($"{policy}:user:{subject}", perUser);
        }

        return new RateLimitBudget($"{policy}:ip:{IpKey(clientIp)}", rule.PermitLimit);
    }

    /// <summary>The <c>sub</c> claim of an authenticated principal; null for an anonymous caller.</summary>
    public static string? SubjectOf(ClaimsPrincipal? user)
        => user?.Identity?.IsAuthenticated == true && user.FindFirst(FreshOrbitClaimTypes.Subject)?.Value is { Length: > 0 } subject ? subject : null;

    internal static string IpKey(IPAddress? clientIp) => clientIp?.ToString() ?? "unknown";
}

internal static class RateLimitingSetup
{
    private const int SlidingSegments = 6;

    /// <summary>
    /// Fixed window per client IP for <c>otp-request</c>, <c>otp-verify</c>, <c>staff-login</c> and <c>refresh</c>;
    /// fixed window per signed-in user, else per client IP with a higher ceiling, for <c>search</c> and <c>public</c>
    /// (<see cref="RateLimitPartitioning"/>); sliding window per user (IP when anonymous) for <c>checkout</c>,
    /// <c>upload</c> and <c>coupon</c>. The client IP is the connection address after <see cref="ForwardedHeadersSetup"/>,
    /// so behind the proxy it is the real client. The user is known because <c>UseRateLimiter</c> runs after
    /// <c>UseAuthentication</c> (Program.cs). Rejections are 429 Problem Details with code RATE_LIMITED.
    /// </summary>
    public static IServiceCollection AddFreshOrbitRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .Validate(o => o.Rules().All(r => r.Rule.PermitLimit > 0 && r.Rule.WindowSeconds > 0), "Every RateLimiting rule needs PermitLimit and WindowSeconds above 0.")
            .Validate(o => o.UserPartitionedRules().All(r => r.Rule.PerUserPermitLimit > 0), "RateLimiting Search and Public need PerUserPermitLimit above 0.")
            .ValidateOnStart();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests.",
                    Detail = "Too many requests; wait before trying again.",
                };
                var seconds = RetryAfterSeconds(context.Lease, context.HttpContext);
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                problem.Extensions["retryAfterSeconds"] = seconds;

                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = problem,
                });
            };

            options.AddPolicy(RateLimitPolicies.OtpRequest, http => FixedWindowByIp(http, Rules(http).OtpRequest, RateLimitPolicies.OtpRequest));
            options.AddPolicy(RateLimitPolicies.OtpVerify, http => FixedWindowByIp(http, Rules(http).OtpVerify, RateLimitPolicies.OtpVerify));
            options.AddPolicy(RateLimitPolicies.Login, http => FixedWindowByIp(http, Rules(http).Login, RateLimitPolicies.Login));
            options.AddPolicy(RateLimitPolicies.Refresh, http => FixedWindowByIp(http, Rules(http).Refresh, RateLimitPolicies.Refresh));
            options.AddPolicy(RateLimitPolicies.Search, http => FixedWindowByUserOrIp(http, Rules(http).Search, RateLimitPolicies.Search));
            options.AddPolicy(RateLimitPolicies.Public, http => FixedWindowByUserOrIp(http, Rules(http).Public, RateLimitPolicies.Public));
            options.AddPolicy(RateLimitPolicies.Checkout, http => SlidingWindowByUser(http, Rules(http).Checkout, RateLimitPolicies.Checkout));
            options.AddPolicy(RateLimitPolicies.Coupon, http => SlidingWindowByUser(http, Rules(http).Coupon, RateLimitPolicies.Coupon));
            options.AddPolicy(RateLimitPolicies.Upload, http => SlidingWindowByUser(http, Rules(http).Upload, RateLimitPolicies.Upload));
        });
        return services;
    }

    /// <summary>
    /// The limiter's own hint when it gives one (fixed window); otherwise one segment of the policy's sliding window
    /// (the earliest a permit can come back).
    /// </summary>
    private static int RetryAfterSeconds(RateLimitLease lease, HttpContext http)
    {
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            return Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        }

        var policy = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        var rule = Rules(http).Rules().FirstOrDefault(r => PolicyOf(r.Name) == policy).Rule;
        return rule is null ? 60 : Math.Max(1, (int)Math.Ceiling(rule.WindowSeconds / (double)SlidingSegments));
    }

    private static string? PolicyOf(string ruleName) => ruleName switch
    {
        nameof(RateLimitingOptions.Checkout) => RateLimitPolicies.Checkout,
        nameof(RateLimitingOptions.Coupon) => RateLimitPolicies.Coupon,
        nameof(RateLimitingOptions.Upload) => RateLimitPolicies.Upload,
        _ => null,
    };

    private static RateLimitingOptions Rules(HttpContext http) => http.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    private static RateLimitPartition<string> FixedWindowByIp(HttpContext http, RateLimitRule rule, string policy)
        => RateLimitPartition.GetFixedWindowLimiter($"{policy}:{ClientIp(http)}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rule.PermitLimit,
            Window = TimeSpan.FromSeconds(rule.WindowSeconds),
            QueueLimit = 0,
        });

    private static RateLimitPartition<string> FixedWindowByUserOrIp(HttpContext http, RateLimitRule rule, string policy)
    {
        var budget = RateLimitPartitioning.ForUserOrIp(policy, rule, http.User, http.Connection.RemoteIpAddress);
        return RateLimitPartition.GetFixedWindowLimiter(budget.PartitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = budget.PermitLimit,
            Window = TimeSpan.FromSeconds(rule.WindowSeconds),
            QueueLimit = 0,
        });
    }

    private static RateLimitPartition<string> SlidingWindowByUser(HttpContext http, RateLimitRule rule, string policy)
    {
        var partition = http.User.FindFirst(FreshOrbitClaimTypes.Subject)?.Value is { } user ? "user:" + user : "ip:" + ClientIp(http);
        return RateLimitPartition.GetSlidingWindowLimiter($"{policy}:{partition}", _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = rule.PermitLimit,
            Window = TimeSpan.FromSeconds(rule.WindowSeconds),
            SegmentsPerWindow = SlidingSegments,
            QueueLimit = 0,
        });
    }

    private static string ClientIp(HttpContext http) => RateLimitPartitioning.IpKey(http.Connection.RemoteIpAddress);
}
