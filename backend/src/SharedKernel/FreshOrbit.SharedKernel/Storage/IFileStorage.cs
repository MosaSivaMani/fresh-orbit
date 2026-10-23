namespace FreshOrbit.SharedKernel.Storage;

/// <summary>
/// Stores files (product images, attachments, invoice PDFs) by key. Keys are relative, slash-separated paths of
/// lowercase letters, digits, <c>-</c>, <c>_</c> and <c>.</c> (see <see cref="StorageKeys"/>), e.g.
/// <c>products/0192…/0193…/640.webp</c>. Modules store only keys; the bytes live behind this interface
/// (local disk in v1, an S3-compatible adapter later — decision D21).
/// </summary>
public interface IFileStorage
{
    /// <summary>Writes (or overwrites) the file at <paramref name="key"/>.</summary>
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Opens the file for reading, or returns null when it does not exist. The caller disposes the stream.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes the file; a missing file is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes every file whose key starts with <paramref name="prefix"/> followed by <c>/</c>.</summary>
    Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>Every file under <paramref name="prefix"/> (recursively), for clean-up jobs.</summary>
    IAsyncEnumerable<StoredFileInfo> ListAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>The public URL of a publicly served key (e.g. <c>/media/products/…/640.webp</c>).</summary>
    string GetPublicUrl(string key);
}

/// <summary>A stored file: its key, size and last write time (UTC).</summary>
public sealed record StoredFileInfo(string Key, long Length, DateTimeOffset LastModifiedAt);
