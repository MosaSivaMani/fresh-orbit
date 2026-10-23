namespace FreshOrbit.SharedKernel.EntityFramework.Platform;

/// <summary>
/// A request made with an <c>Idempotency-Key</c>. <see cref="StatusCode"/> is null while the first request is
/// still running; afterwards it holds the stored response that retries replay.
/// </summary>
public sealed class IdempotencyRecord
{
    public IdempotencyRecord(string scope, string key, string requestHash, DateTimeOffset expiresAt)
    {
        Scope = scope;
        Key = key;
        RequestHash = requestHash;
        ExpiresAt = expiresAt;
    }

    public string Scope { get; private set; }

    public string Key { get; private set; }

    public string RequestHash { get; private set; }

    public int? StatusCode { get; private set; }

    /// <summary>jsonb document with content type, body and replayed headers.</summary>
    public string? Response { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }
}
