using System.Globalization;
using FreshOrbit.SharedKernel.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.SharedKernel.Web.Errors;

/// <summary>
/// Maps exceptions to RFC 7807 Problem Details with the extensions <c>code</c>, <c>errors</c>
/// (field → [{code, message}]) and <c>traceId</c>:
/// <see cref="RefusalException"/>/<see cref="DomainException"/> → 422, <see cref="NotFoundException"/> → 404,
/// <see cref="ConflictException"/>/<see cref="ConcurrentSaveException"/> → 409, <see cref="ConcurrencyException"/> → 412,
/// <see cref="VersionRequiredException"/> → 428, <see cref="UnauthorizedException"/> → 401, <see cref="ForbiddenException"/> → 403,
/// <see cref="TooManyRequestsException"/> → 429 (+ <c>Retry-After</c>), <see cref="ServiceUnavailableException"/> → 503 (D31),
/// <see cref="BadHttpRequestException"/> → 400, anything else → 500.
/// </summary>
public sealed partial class FreshOrbitExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<FreshOrbitExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var problem = ToProblem(exception);
        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(exception, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        if (exception is TooManyRequestsException { RetryAfterSeconds: { } retryAfter })
        {
            httpContext.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    public static ProblemDetails ToProblem(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            RefusalException refusal => Create(StatusCodes.Status422UnprocessableEntity, "The request was refused.", refusal.Code, refusal.Message, Group(refusal.Errors)),
            NotFoundException notFound => Create(StatusCodes.Status404NotFound, "Not found.", notFound.Code, notFound.Message, FieldErrors(notFound)),
            ConcurrencyException stale => Create(StatusCodes.Status412PreconditionFailed, "The version is stale.", stale.Code, stale.Message, FieldErrors(stale)),
            ConcurrentSaveException race => Create(StatusCodes.Status409Conflict, "Concurrent save.", race.Code, race.Message, null),
            ConflictException conflict => Create(StatusCodes.Status409Conflict, "Conflict.", conflict.Code, conflict.Message, FieldErrors(conflict)),
            UnauthorizedException unauthorized => Create(StatusCodes.Status401Unauthorized, "Unauthenticated.", unauthorized.Code, unauthorized.Message, FieldErrors(unauthorized)),
            ForbiddenException forbidden => Create(StatusCodes.Status403Forbidden, "Forbidden.", forbidden.Code, forbidden.Message, FieldErrors(forbidden)),
            TooManyRequestsException tooMany => WithRetryAfter(Create(StatusCodes.Status429TooManyRequests, "Too many requests.", tooMany.Code, tooMany.Message, null), tooMany.RetryAfterSeconds),
            ServiceUnavailableException unavailable => Create(StatusCodes.Status503ServiceUnavailable, "Service unavailable.", unavailable.Code, unavailable.Message, null),
            VersionRequiredException missing => Create(StatusCodes.Status428PreconditionRequired, "Version required.", missing.Code, missing.Message, FieldErrors(missing)),
            DomainException domain => Create(StatusCodes.Status422UnprocessableEntity, "The request was refused.", domain.Code, domain.Message, FieldErrors(domain)),
            BadHttpRequestException bad => Create(bad.StatusCode, "The request could not be read.", ProblemCodes.ForStatus(bad.StatusCode), bad.Message, null),
            _ => Create(StatusCodes.Status500InternalServerError, "An unexpected error occurred.", ProblemCodes.InternalError, null, null),
        };
    }

    private static ProblemDetails Create(int status, string title, string code, string? detail, Dictionary<string, ProblemError[]>? errors)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        if (errors is { Count: > 0 })
        {
            problem.Extensions["errors"] = errors;
        }

        return problem;
    }

    private static ProblemDetails WithRetryAfter(ProblemDetails problem, int? retryAfterSeconds)
    {
        if (retryAfterSeconds is { } seconds)
        {
            problem.Extensions["retryAfterSeconds"] = seconds;
        }

        return problem;
    }

    private static Dictionary<string, ProblemError[]>? FieldErrors(DomainException exception)
        => exception.Field is null
            ? null
            : new Dictionary<string, ProblemError[]> { [exception.Field] = [new ProblemError(exception.Code, exception.Message)] };

    private static Dictionary<string, ProblemError[]> Group(IEnumerable<FieldError> errors)
        => errors.GroupBy(e => e.Field, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => new ProblemError(e.Code, e.Message)).ToArray(), StringComparer.Ordinal);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private partial void LogUnhandled(Exception exception, string method, string path);
}
