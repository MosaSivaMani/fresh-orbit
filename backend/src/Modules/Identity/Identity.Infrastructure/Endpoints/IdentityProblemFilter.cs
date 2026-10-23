using System.Globalization;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Identity.Infrastructure.Endpoints;

/// <summary>
/// Gives Identity refusals the status docs/api §2 assigns them (401, 403, 429) instead of the shared handler's 422.
/// Workaround until the shared exception handler supports a status per code (see report).
/// </summary>
internal sealed class IdentityProblemFilter : IEndpointFilter
{
    private static readonly Dictionary<string, int> Statuses = new(StringComparer.Ordinal)
    {
        [IdentityErrors.InvalidCredentials] = StatusCodes.Status401Unauthorized,
        [IdentityErrors.RefreshTokenInvalid] = StatusCodes.Status401Unauthorized,
        [IdentityErrors.RefreshTokenReused] = StatusCodes.Status401Unauthorized,
        [IdentityErrors.AccountLocked] = StatusCodes.Status403Forbidden,
        [IdentityErrors.AccountDisabled] = StatusCodes.Status403Forbidden,
        [IdentityErrors.OtpTooManyAttempts] = StatusCodes.Status429TooManyRequests,
        [IdentityErrors.OtpResendTooSoon] = StatusCodes.Status429TooManyRequests,
        [IdentityErrors.RateLimited] = StatusCodes.Status429TooManyRequests,
    };

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(context);
        }
        catch (DomainException exception) when (exception is not RefusalException && Statuses.ContainsKey(exception.Code))
        {
            var status = Statuses[exception.Code];
            var problem = new ProblemDetails
            {
                Status = status,
                Title = status switch
                {
                    StatusCodes.Status401Unauthorized => "Unauthenticated.",
                    StatusCodes.Status403Forbidden => "Forbidden.",
                    _ => "Too many requests.",
                },
                Detail = exception.Message,
            };
            problem.Extensions["code"] = exception.Code;
            if (exception is RetryLaterException retry)
            {
                problem.Extensions["retryAfterSeconds"] = retry.RetryAfterSeconds;
                context.HttpContext.Response.Headers.RetryAfter = retry.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            }

            return Results.Problem(problem);
        }
    }
}

internal static class IdentityEndpointExtensions
{
    public const string CustomerCode = "CUSTOMER";
    public const string StaffCode = "STAFF";

    /// <summary>Only a customer token passes (staff and anonymous callers get 403 / 401).</summary>
    public static TBuilder RequireCustomer<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim(FreshOrbitClaimTypes.ActorType, CustomerCode));

    /// <summary>Only a staff token passes.</summary>
    public static TBuilder RequireStaff<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim(FreshOrbitClaimTypes.ActorType, StaffCode));

    public static RouteGroupBuilder WithIdentityProblems(this RouteGroupBuilder builder) => builder.AddEndpointFilter<IdentityProblemFilter>();
}
