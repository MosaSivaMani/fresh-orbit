namespace FreshOrbit.SharedKernel.Errors;

/// <summary>A dependency (payment provider, SMS gateway) is disabled or unreachable (HTTP 503, D31); the client may retry later.</summary>
public class ServiceUnavailableException : DomainException
{
    public const string DefaultCode = "SERVICE_UNAVAILABLE";

    public ServiceUnavailableException(string code, string message, Exception? innerException = null)
        : base(code, message, null, innerException)
    {
    }
}
