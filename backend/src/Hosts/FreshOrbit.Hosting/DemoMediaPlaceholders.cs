using System.Collections.Concurrent;
using System.Globalization;
using FreshOrbit.Catalog.Infrastructure.Seeding;
using SkiaSharp;

namespace FreshOrbit.Hosting;

/// <summary>
/// Demo only (<see cref="DemoSetup.UseFreshOrbitDemoMedia"/>): for an image key whose file is gone, the sample set's
/// committed picture for that key (<see cref="SampleImageFiles"/>, read once) when there is one, else the sample
/// placeholder drawing. Drawing sizes follow the key: banners 1600×600, categories 1200×800, product renditions
/// (<c>…/640.webp</c>) square at the nearest rendition width, anything else 640×640. Each size and format is rendered once.
/// </summary>
internal sealed class DemoMediaPlaceholders(string? sampleDirectory)
{
    private static readonly int[] RenditionWidths = [320, 640, 1280];

    private readonly ConcurrentDictionary<(PlaceholderShape Shape, int Width, int Height, SKEncodedImageFormat Format), byte[]> _rendered = new();

    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public static bool TryGetFormat(string key, out SKEncodedImageFormat format, out string contentType)
    {
        switch (Path.GetExtension(key).ToUpperInvariant())
        {
            case ".WEBP":
                format = SKEncodedImageFormat.Webp;
                contentType = "image/webp";
                return true;
            case ".PNG":
                format = SKEncodedImageFormat.Png;
                contentType = "image/png";
                return true;
            case ".JPG" or ".JPEG":
                format = SKEncodedImageFormat.Jpeg;
                contentType = "image/jpeg";
                return true;
            default:
                format = default;
                contentType = string.Empty;
                return false;
        }
    }

    public static (int Width, int Height) SizeFor(string key)
    {
        if (key.Contains("/banners/", StringComparison.OrdinalIgnoreCase))
        {
            return (1600, 600);
        }

        if (key.Contains("/categories/", StringComparison.OrdinalIgnoreCase))
        {
            return (1200, 800);
        }

        if (int.TryParse(Path.GetFileNameWithoutExtension(key), NumberStyles.None, CultureInfo.InvariantCulture, out var requested))
        {
            var width = RenditionWidths.MinBy(w => Math.Abs(w - requested));
            return (width, width);
        }

        return (640, 640);
    }

    public byte[] For(string key, SKEncodedImageFormat format)
    {
        if (SampleImageFiles.Find(sampleDirectory, key) is { } file
            && string.Equals(Path.GetExtension(file), Path.GetExtension(key), StringComparison.OrdinalIgnoreCase))
        {
            return _files.GetOrAdd(file, File.ReadAllBytes);
        }

        var (width, height) = SizeFor(key);
        var shape = SamplePlaceholderRenderer.ShapeFor(key);
        return _rendered.GetOrAdd((shape, width, height, format), static k => SamplePlaceholderRenderer.Render(k.Width, k.Height, k.Shape, k.Format));
    }
}
