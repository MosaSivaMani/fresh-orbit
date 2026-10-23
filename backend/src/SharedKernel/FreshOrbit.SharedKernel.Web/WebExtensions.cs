using System.Diagnostics;
using FreshOrbit.SharedKernel.Serialization;
using FreshOrbit.SharedKernel.Web.Errors;
using FreshOrbit.SharedKernel.Web.Idempotency;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.SharedKernel.Web;

public static class WebExtensions
{
    /// <summary>
    /// Problem Details (with <c>code</c> and <c>traceId</c> on every error response), the exception handler,
    /// UPPER_SNAKE enum JSON, and binding failures surfaced as 400 MALFORMED_REQUEST.
    /// </summary>
    public static IServiceCollection AddFreshOrbitWeb(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            if (!problem.Extensions.ContainsKey("code"))
            {
                problem.Extensions["code"] = ProblemCodes.ForStatus(problem.Status ?? context.HttpContext.Response.StatusCode);
            }
        });
        services.AddExceptionHandler<FreshOrbitExceptionHandler>();
        services.ConfigureHttpJsonOptions(options => FreshOrbitJson.Configure(options.SerializerOptions));
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        return services;
    }

    /// <summary>Exception handler, Problem Details for empty error responses, and idempotency body buffering.</summary>
    public static IApplicationBuilder UseFreshOrbitWeb(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseIdempotencyBuffering();
        return app;
    }
}
