using FreshOrbit.Catalog.Application.Admin;
using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.Catalog.Public;
using FreshOrbit.SharedKernel.Errors;
using SkiaSharp;

namespace FreshOrbit.Catalog.Infrastructure.Images;

/// <summary>
/// CAT-R16 with SkiaSharp (MIT): decodes JPEG/PNG/WebP, applies the EXIF orientation, refuses images whose shortest
/// side is under 800 px, and re-encodes WebP renditions 320/640/1280 px wide (never upscaled). Re-encoding drops EXIF.
/// SEC-5: the dimensions in the header are checked before any pixel memory is allocated (decompression bombs), and
/// formats that can (JPEG) are decoded straight at the smallest supported scale that still covers the largest rendition.
/// The same pipeline serves other modules through <see cref="IImageSanitizer"/> (W12 complaint photos).
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor, IImageSanitizer
{
    public const int WebPQuality = 82;

    public IReadOnlyList<ImageRendition> CreateRenditions(byte[] original)
    {
        ArgumentNullException.ThrowIfNull(original);
        using var source = Decode(original, ImageFormats.MinShortestSide, ImageRenditions.Widths[^1]);
        var renditions = new List<ImageRendition>(ImageRenditions.Widths.Count);
        foreach (var target in ImageRenditions.Widths)
        {
            renditions.Add(Encode(source, target));
        }

        return renditions;
    }

    public SanitizedImage ToWebP(byte[] bytes, int minShortestSide, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(minShortestSide, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWidth, 1);
        if (ImageFormats.Detect(bytes) is null)
        {
            throw Invalid("Upload a JPEG, PNG or WebP image.");
        }

        using var source = Decode(bytes, minShortestSide, maxWidth);
        var rendition = Encode(source, maxWidth);
        return new SanitizedImage(rendition.Width, rendition.Height, rendition.WebP);
    }

    private static ImageRendition Encode(SKBitmap source, int target)
    {
        var width = Math.Min(target, source.Width);
        var height = Math.Max(1, (int)Math.Round((double)source.Height * width / source.Width));
        using var resized = width == source.Width
            ? source.Copy()
            : source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKCubicResampler.Mitchell));
        if (resized is null)
        {
            throw Invalid("The image could not be resized.");
        }

        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Webp, WebPQuality)
            ?? throw new InvalidOperationException("WebP encoding is not available in this SkiaSharp build.");
        return new ImageRendition(width, height, data.ToArray());
    }

    private static SKBitmap Decode(byte[] bytes, int minShortestSide, int largestWidth)
    {
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream) ?? throw Invalid("The file is not a readable JPEG, PNG or WebP image.");
        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (width > ImageFormats.MaxSide || height > ImageFormats.MaxSide || (long)width * height > ImageFormats.MaxPixels)
        {
            throw Invalid($"Upload an image of at most {ImageFormats.MaxSide} px a side and {ImageFormats.MaxPixels / 1_000_000} megapixels.");
        }

        if (Math.Min(width, height) < minShortestSide)
        {
            throw Invalid($"Upload an image at least {minShortestSide} px on its shortest side.");
        }

        var size = ScaledSize(codec, largestWidth);
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw Invalid("The file is not a readable JPEG, PNG or WebP image.");
        }

        return Orient(bitmap, codec.EncodedOrigin);
    }

    /// <summary>
    /// The smallest size the codec can decode to directly whose upright width still covers the largest rendition;
    /// the full size when the codec cannot scale (PNG, WebP return their own size).
    /// </summary>
    private static SKSizeI ScaledSize(SKCodec codec, int largest)
    {
        var full = codec.Info.Size;
        var swap = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var uprightWidth = swap ? full.Height : full.Width;
        if (uprightWidth <= largest)
        {
            return full;
        }

        var scaled = codec.GetScaledDimensions((float)largest / uprightWidth);
        var scaledUprightWidth = swap ? scaled.Height : scaled.Width;
        return scaled.Width > 0 && scaled.Height > 0 && scaledUprightWidth >= largest ? scaled : full;
    }

    private static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default)
        {
            return bitmap;
        }

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var oriented = new SKBitmap(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height, bitmap.ColorType, bitmap.AlphaType);
        float w = bitmap.Width;
        float h = bitmap.Height;

        // Maps source (x, y) to the upright image: x' = scaleX·x + skewX·y + transX, y' = skewY·x + scaleY·y + transY.
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            _ => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        };
        using (var canvas = new SKCanvas(oriented))
        {
            using var image = SKImage.FromBitmap(bitmap);
            canvas.SetMatrix(matrix);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        }

        bitmap.Dispose();
        return oriented;
    }

    private static RefusalException Invalid(string message) => new(new FieldError("file", CatalogErrors.ImageInvalid, message));
}
