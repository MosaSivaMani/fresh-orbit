using FreshOrbit.Catalog.Application.Admin;
using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using SkiaSharp;

namespace FreshOrbit.Catalog.Infrastructure.Images;

/// <summary>
/// CAT-R16 with SkiaSharp (MIT): decodes JPEG/PNG/WebP, applies the EXIF orientation, refuses images whose shortest
/// side is under 800 px, and re-encodes WebP renditions 320/640/1280 px wide (never upscaled). Re-encoding drops EXIF.
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor
{
    public const int WebPQuality = 82;

    public IReadOnlyList<ImageRendition> CreateRenditions(byte[] original)
    {
        ArgumentNullException.ThrowIfNull(original);
        using var source = Decode(original);
        if (Math.Min(source.Width, source.Height) < ImageFormats.MinShortestSide)
        {
            throw Invalid($"Upload an image at least {ImageFormats.MinShortestSide} px on its shortest side.");
        }

        var renditions = new List<ImageRendition>(ImageRenditions.Widths.Count);
        foreach (var target in ImageRenditions.Widths)
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
            renditions.Add(new ImageRendition(width, height, data.ToArray()));
        }

        return renditions;
    }

    private static SKBitmap Decode(byte[] bytes)
    {
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream) ?? throw Invalid("The file is not a readable JPEG, PNG or WebP image.");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw Invalid("The file is not a readable JPEG, PNG or WebP image.");
        }

        return Orient(bitmap, codec.EncodedOrigin);
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
