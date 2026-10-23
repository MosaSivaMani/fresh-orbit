namespace FreshOrbit.SharedKernel.Errors;

/// <summary>The addressed resource does not exist or is not visible to the caller (HTTP 404).</summary>
public sealed class NotFoundException : DomainException
{
    public const string DefaultCode = "NOT_FOUND";

    public NotFoundException(string message)
        : base(DefaultCode, message)
    {
    }

    public NotFoundException(string code, string message)
        : base(code, message)
    {
    }
}
