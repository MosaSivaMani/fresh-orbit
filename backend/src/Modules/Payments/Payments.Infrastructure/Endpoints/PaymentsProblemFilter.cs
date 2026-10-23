using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Providers;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Errors;
using Microsoft.AspNetCore.Http;

namespace FreshOrbit.Payments.Infrastructure.Endpoints;

/// <summary>
/// Maps the Payments refusals that are not 422 to their HTTP status: 401 <c>WEBHOOK_SIGNATURE_INVALID</c>,
/// 403 <c>LOCATION_ACCESS_DENIED</c>, 503 <c>PAYMENT_PROVIDER_UNAVAILABLE</c>. (The shared exception handler maps every
/// <see cref="DomainException"/> to 422; a shared Forbidden/Unavailable exception would make this filter unnecessary.)
/// Add it before <c>WithIdempotency</c> so the key is released first.
/// </summary>
public sealed class PaymentsProblemFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(context);
        }
        catch (DomainException exception) when (StatusFor(exception) is { } status)
        {
            return ToProblem(status, exception);
        }
    }

    public static int? StatusFor(DomainException exception) => exception switch
    {
        WebhookSignatureInvalidException => StatusCodes.Status401Unauthorized,
        LocationAccessDeniedException => StatusCodes.Status403Forbidden,
        PaymentProviderUnavailableException => StatusCodes.Status503ServiceUnavailable,
        _ => null,
    };

    public static IResult ToProblem(int status, DomainException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var problem = FreshOrbitExceptionHandler.ToProblem(exception);
        return TypedResults.Problem(
            detail: problem.Detail,
            statusCode: status,
            title: status switch
            {
                StatusCodes.Status401Unauthorized => "Unauthorized.",
                StatusCodes.Status403Forbidden => "Forbidden.",
                _ => "Service unavailable.",
            },
            extensions: problem.Extensions);
    }
}
