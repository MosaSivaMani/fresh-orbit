namespace FreshOrbit.SharedKernel.Errors;

/// <summary>The caller is not (or no longer) authenticated for this request (HTTP 401), e.g. wrong password, invalid token (D31).</summary>
public class UnauthorizedException : DomainException
{
    public const string DefaultCode = "UNAUTHENTICATED";

    public UnauthorizedException(string code, string message, string? field = null)
        : base(code, message, field)
    {
    }
}
