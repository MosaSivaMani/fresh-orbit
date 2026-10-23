namespace FreshOrbit.SharedKernel.Errors;

/// <summary>The request conflicts with the current state of a resource (HTTP 409).</summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string code, string message, string? field = null)
        : base(code, message, field)
    {
    }
}
