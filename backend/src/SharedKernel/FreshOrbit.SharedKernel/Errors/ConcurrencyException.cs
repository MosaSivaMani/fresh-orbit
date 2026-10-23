namespace FreshOrbit.SharedKernel.Errors;

/// <summary>The If-Match version quoted by the client is stale (HTTP 412 CONCURRENCY_CONFLICT).</summary>
public sealed class ConcurrencyException : DomainException
{
    public const string DefaultCode = "CONCURRENCY_CONFLICT";

    public ConcurrencyException(uint expectedVersion, uint currentVersion)
        : base(DefaultCode, "The record was changed by someone else; reload it and send the current version in If-Match.", "If-Match")
    {
        ExpectedVersion = expectedVersion;
        CurrentVersion = currentVersion;
    }

    public uint ExpectedVersion { get; }

    public uint CurrentVersion { get; }
}
