namespace FreshOrbit.SharedKernel.Errors;

/// <summary>
/// A business rule refused the request (HTTP 422). <see cref="Code"/> is UPPER_SNAKE and stable;
/// <see cref="Field"/> names the request field to fix when there is one.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message, string? field = null)
        : base(message)
    {
        ErrorCodes.EnsureValid(code);
        Code = code;
        Field = field;
    }

    public DomainException(string code, string message, string? field, Exception? innerException)
        : base(message, innerException)
    {
        ErrorCodes.EnsureValid(code);
        Code = code;
        Field = field;
    }

    public string Code { get; }

    public string? Field { get; }
}
