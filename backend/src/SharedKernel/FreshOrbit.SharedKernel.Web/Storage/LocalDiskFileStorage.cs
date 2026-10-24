using System.Runtime.CompilerServices;
using FreshOrbit.SharedKernel.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FreshOrbit.SharedKernel.Web.Storage;

/// <summary>Configuration section <c>FileStorage</c> (decision D21).</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Folder holding the files (a Docker volume in production). Required in Production; elsewhere an empty value
    /// means <c>&lt;local application data&gt;/freshorbit/media</c>.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>URL prefix the Api serves files under; also the prefix of <see cref="IFileStorage.GetPublicUrl"/>.</summary>
    public string PublicBaseUrl { get; set; } = FileStorageExtensions.MediaRequestPath;
}

/// <summary>
/// <see cref="IFileStorage"/> on the local file system. A key maps to <c>RootPath/key</c>; writes go to a temporary
/// file first and are moved into place, so readers never see a half-written file.
/// </summary>
public sealed class LocalDiskFileStorage : IFileStorage
{
    private readonly string _rootPath;
    private readonly string _publicBaseUrl;

    public LocalDiskFileStorage(IOptions<FileStorageOptions> options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        _rootPath = ResolveRootPath(options.Value, environment);
        _publicBaseUrl = ResolvePublicBaseUrl(options.Value, environment);
    }

    /// <summary>The absolute folder files are stored in.</summary>
    public string RootPath => _rootPath;

    public static string ResolveRootPath(FileStorageOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        if (!string.IsNullOrWhiteSpace(options.RootPath))
        {
            return Path.GetFullPath(options.RootPath, environment.ContentRootPath);
        }

        if (environment.IsProduction())
        {
            throw new InvalidOperationException($"{FileStorageOptions.SectionName}:{nameof(FileStorageOptions.RootPath)} must be configured in Production.");
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "freshorbit", "media");
    }

    /// <summary>
    /// The prefix of public URLs without a trailing slash (W15). In Production it must be an absolute HTTPS URL (e.g.
    /// <c>https://api.&lt;domain&gt;/media</c>) so every client gets absolute image URLs; elsewhere the default
    /// <c>/media</c> path is allowed.
    /// </summary>
    public static string ResolvePublicBaseUrl(FileStorageOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        var baseUrl = options.PublicBaseUrl.Trim().TrimEnd('/');
        if (environment.IsProduction()
            && !(Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{FileStorageOptions.SectionName}:{nameof(FileStorageOptions.PublicBaseUrl)} must be an absolute https URL in Production, e.g. https://api.example.com/media.");
        }

        return baseUrl;
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(file, cancellationToken);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = PathFor(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(PathFor(key)));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var path = PathFor(prefix);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<StoredFileInfo> ListAsync(string prefix, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var directory = PathFor(prefix);
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (file.EndsWith(".tmp", StringComparison.Ordinal))
            {
                continue;
            }

            var info = new FileInfo(file);
            var key = Path.GetRelativePath(_rootPath, file).Replace(Path.DirectorySeparatorChar, '/');
            yield return new StoredFileInfo(key, info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
            await Task.Yield();
        }
    }

    public string GetPublicUrl(string key) => $"{_publicBaseUrl}/{StorageKeys.EnsureValid(key)}";

    private string PathFor(string key)
    {
        StorageKeys.EnsureValid(key);
        var path = Path.GetFullPath(Path.Combine(_rootPath, key.Replace('/', Path.DirectorySeparatorChar)));
        return path.StartsWith(_rootPath, StringComparison.Ordinal)
            ? path
            : throw new ArgumentException($"'{key}' points outside the storage root.", nameof(key));
    }
}
