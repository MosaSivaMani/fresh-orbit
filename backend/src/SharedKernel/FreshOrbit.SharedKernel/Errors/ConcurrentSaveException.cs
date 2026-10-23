namespace FreshOrbit.SharedKernel.Errors;

/// <summary>Another transaction saved the same rows first; the command lost the race (HTTP 409 CONCURRENT_SAVE).</summary>
public sealed class ConcurrentSaveException : DomainException
{
    public const string DefaultCode = "CONCURRENT_SAVE";

    public ConcurrentSaveException(Exception? innerException)
        : base(DefaultCode, "Another change was saved at the same time; retry the request.", null, innerException)
    {
    }
}
