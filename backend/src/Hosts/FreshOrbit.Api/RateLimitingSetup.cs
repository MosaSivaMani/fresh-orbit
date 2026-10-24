using System.Threading.RateLimiting;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Api;

/// <summary>Configuration section <c>RateLimiting</c>: permits per window for each policy (docs/api §1.9).</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitRule OtpRequest { get; set; } = new() { PermitLimit = 20, WindowSeconds = 3600 };

    public RateLimitRule OtpVerify { get; set; } = new() { PermitLimit = 30, WindowSeconds = 600 };

    public RateLimitRule Login { get; set; } = new() { PermitLimit = 10, WindowSeconds = 300 };

    public RateLimitRule Refresh { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    public RateLimitRule Checkout { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    public RateLimitRule Search { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    public RateLimitRule Public { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

    public RateLimitRule Coupon { get; set; } = new() { PermitLimit = 10, WindowSeconds = 600 };

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
    }
}

public sealed class RateLimitRule
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }
}

internal static class RateLimitingSetup
{
    private const int SlidingSegments = 6;

    /// <summary>
    /// Fixed window per client IP for <c>otp-request</c>, <c>otp-verify</c>, <c>staff-login</c>, <c>refresh</c>,
    /// <c>search</c> and <c>public</c>; sliding window per user (IP when anonymous) for <c>checkout</c> and
    /// <c>coupon</c>. The client IP is the connection address after <see cref="ForwardedHeadersSetup"/>, so behind
    /// the proxy it is the real client. Rejections are 429 Problem Details with code RATE_LIMITED.
    /// </summary>
    public static IServiceCollection AddFreshOrbitRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .Validate(o => o.Rules().All(r => r.Rule.PermitLimit > 0 && r.Rule.WindowSeconds > 0), "Every RateLimiting rule needs PermitLimit and WindowSeconds above 0.")
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
            options.AddPolicy(RateLimitPolicies.Search, http => FixedWindowByIp(http, Rules(http).Search, RateLimitPolicies.Search));
            options.AddPolicy(RateLimitPolicies.Public, http => FixedWindowByIp(http, Rules(http).Public, RateLimitPolicies.Public));
            options.AddPolicy(RateLimitPolicies.Checkout, http => SlidingWindowByUser(http, Rules(http).Checkout, RateLimitPolicies.Checkout));
            options.AddPolicy(RateLimitPolicies.Coupon, http => SlidingWindowByUser(http, Rules(http).Coupon, RateLimitPolicies.Coupon));
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

    private static string ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
