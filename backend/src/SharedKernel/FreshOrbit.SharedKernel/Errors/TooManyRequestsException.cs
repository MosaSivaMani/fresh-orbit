namespace FreshOrbit.SharedKernel.Errors;

/// <summary>
/// The caller asked too often (HTTP 429, D31). With <see cref="RetryAfterSeconds"/> the response carries the
/// <c>Retry-After</c> header and the <c>retryAfterSeconds</c> extension.
/// </summary>
public class TooManyRequestsException : DomainException
{
    public const string DefaultCode = "RATE_LIMITED";

    public TooManyRequestsException(string code, string message, int? retryAfterSeconds = null)
        : base(code, message)
    {
        RetryAfterSeconds = retryAfterSeconds is { } seconds ? Math.Max(1, seconds) : null;
    }

    /// <summary>Seconds the client should wait before retrying; null when unknown.</summary>
    public int? RetryAfterSeconds { get; }
}
