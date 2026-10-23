using System.Threading.RateLimiting;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Api;

/// <summary>Configuration section <c>RateLimiting</c>: permits per window for each policy.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitRule Otp { get; set; } = new() { PermitLimit = 5, WindowSeconds = 600 };

    public RateLimitRule Login { get; set; } = new() { PermitLimit = 10, WindowSeconds = 300 };

    public RateLimitRule Checkout { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
}

public sealed class RateLimitRule
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }
}

internal static class RateLimitingSetup
{
    /// <summary>
    /// <c>otp</c> and <c>login</c>: fixed window per client IP; <c>checkout</c>: sliding window per user (IP when
    /// anonymous). Rejections are 429 Problem Details with code RATE_LIMITED.
    /// </summary>
    public static IServiceCollection AddFreshOrbitRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>().BindConfiguration(RateLimitingOptions.SectionName);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests.",
                        Detail = "Too many requests; wait before trying again.",
                    },
                });
            };

            options.AddPolicy(RateLimitPolicies.Otp, http => FixedWindowByIp(http, Rules(http).Otp, RateLimitPolicies.Otp));
            options.AddPolicy(RateLimitPolicies.Login, http => FixedWindowByIp(http, Rules(http).Login, RateLimitPolicies.Login));
            options.AddPolicy(RateLimitPolicies.Checkout, http =>
            {
                var rule = Rules(http).Checkout;
                var partition = http.User.FindFirst(FreshOrbitClaimTypes.Subject)?.Value ?? ClientIp(http);
                return RateLimitPartition.GetSlidingWindowLimiter($"{RateLimitPolicies.Checkout}:{partition}", _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = rule.PermitLimit,
                    Window = TimeSpan.FromSeconds(rule.WindowSeconds),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                });
            });
        });
        return services;
    }

    private static RateLimitingOptions Rules(HttpContext http) => http.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    private static RateLimitPartition<string> FixedWindowByIp(HttpContext http, RateLimitRule rule, string policy)
        => RateLimitPartition.GetFixedWindowLimiter($"{policy}:{ClientIp(http)}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rule.PermitLimit,
            Window = TimeSpan.FromSeconds(rule.WindowSeconds),
            QueueLimit = 0,
        });

    private static string ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
