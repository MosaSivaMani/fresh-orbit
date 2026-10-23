namespace FreshOrbit.SharedKernel.Errors;

/// <summary>The caller is authenticated but may not do this (HTTP 403), e.g. another location, locked account (D31).</summary>
public class ForbiddenException : DomainException
{
    public const string DefaultCode = "FORBIDDEN";

    public ForbiddenException(string code, string message, string? field = null)
        : base(code, message, field)
    {
    }
}
