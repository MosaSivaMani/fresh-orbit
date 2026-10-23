namespace FreshOrbit.SharedKernel.Errors;

/// <summary>Refusal with one or more field-keyed reasons (HTTP 422).</summary>
public sealed class RefusalException : DomainException
{
    public const string DefaultCode = "VALIDATION_FAILED";

    public RefusalException(IReadOnlyList<FieldError> errors)
        : this(DefaultCode, "The request was refused; see errors for the fields to fix.", errors)
    {
    }

    public RefusalException(string code, string message, IReadOnlyList<FieldError> errors)
        : base(code, message)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = errors;
    }

    public RefusalException(FieldError error)
        : this(error?.Code ?? DefaultCode, error?.Message ?? string.Empty, [error!])
    {
    }

    public IReadOnlyList<FieldError> Errors { get; }
}
