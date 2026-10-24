using Microsoft.AspNetCore.Http;

namespace FreshOrbit.SharedKernel.Web.Errors;

/// <summary>Codes used in Problem Details when no more specific code applies.</summary>
public static class ProblemCodes
{
    public const string MalformedRequest = "MALFORMED_REQUEST";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string Conflict = "CONFLICT";
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";
    public const string RateLimited = "RATE_LIMITED";
    public const string InternalError = "INTERNAL_ERROR";
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string IdempotencyKeyInProgress = "IDEMPOTENCY_KEY_IN_PROGRESS";
    public const string IdempotencyKeyInvalid = "IDEMPOTENCY_KEY_INVALID";

    public static string ForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => MalformedRequest,
        StatusCodes.Status401Unauthorized => Unauthenticated,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
        StatusCodes.Status409Conflict => Conflict,
        StatusCodes.Status413PayloadTooLarge => PayloadTooLarge,
        StatusCodes.Status429TooManyRequests => RateLimited,
        >= 500 => InternalError,
        _ => MalformedRequest,
    };
}
