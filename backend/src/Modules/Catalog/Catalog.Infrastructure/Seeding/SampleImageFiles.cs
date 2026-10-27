namespace FreshOrbit.Catalog.Infrastructure.Seeding;

/// <summary>
/// Real picture files of the sample set (docs/product/sample-data.md): the file for image key <c>sample/{path}</c> is
/// <c>{sample directory}/images/{path}</c>. A key without a file gets the placeholder drawing instead.
/// </summary>
public static class SampleImageFiles
{
    private const string KeyPrefix = "sample/";

    /// <summary>The full path of the committed file for <paramref name="imageKey"/>, or null when there is none.</summary>
    public static string? Find(string? sampleDirectory, string imageKey)
    {
        ArgumentNullException.ThrowIfNull(imageKey);
        var key = imageKey.TrimStart('/');
        if (string.IsNullOrWhiteSpace(sampleDirectory)
            || !key.StartsWith(KeyPrefix, StringComparison.Ordinal)
            || key.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var root = Path.GetFullPath(Path.Combine(sampleDirectory, "images"));
        var path = Path.GetFullPath(Path.Combine(root, key[KeyPrefix.Length..]));
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(path) ? path : null;
    }
}
