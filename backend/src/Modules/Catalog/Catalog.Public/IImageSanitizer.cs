namespace FreshOrbit.Catalog.Public;

/// <summary>
/// The Catalog image pipeline offered to other modules (W12 complaint photos), so uploads from any module pass the same
/// checks instead of a copy of them: JPEG, PNG or WebP by magic bytes; SEC-5 header limits (at most
/// <see cref="ImageLimits.MaxSide"/> px a side and <see cref="ImageLimits.MaxPixels"/> pixels) checked before any pixel
/// memory is allocated; EXIF orientation applied; re-encoded to one WebP, which drops EXIF and every other metadata
/// block (GPS included), so callers never need to keep the original (SEC-20).
/// </summary>
public interface IImageSanitizer
{
    /// <summary>
    /// The upright image re-encoded as WebP, at most <paramref name="maxWidth"/> px wide (never upscaled). Throws
    /// <c>RefusalException</c> with field <c>file</c> and code <c>IMAGE_INVALID</c> when the bytes are not a readable
    /// JPEG, PNG or WebP, claim more than the SEC-5 limits, or are under <paramref name="minShortestSide"/> px on their
    /// shortest side.
    /// </summary>
    SanitizedImage ToWebP(byte[] bytes, int minShortestSide, int maxWidth);
}

/// <summary>A re-encoded image: WebP bytes without metadata and their size in px.</summary>
public sealed record SanitizedImage(int Width, int Height, byte[] WebP)
{
    public const string ContentType = "image/webp";
}

/// <summary>Limits shared by every image upload (CAT-R16, SEC-5).</summary>
public static class ImageLimits
{
    /// <summary>Largest compressed upload accepted (5 MB).</summary>
    public const int MaxBytes = 5 * 1024 * 1024;

    /// <summary>SEC-5: longest side the header may claim.</summary>
    public const int MaxSide = 8000;

    /// <summary>SEC-5: most pixels the header may claim (40 MP, about 160 MB decoded at 4 bytes per pixel).</summary>
    public const long MaxPixels = 40_000_000;
}
