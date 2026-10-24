using System.Globalization;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.Catalog.Domain.Products.Events;
using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Events;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Storage;

namespace FreshOrbit.Catalog.Application.Admin;

public static class CatalogFormats
{
    public static string Version(uint version) => version.ToString(CultureInfo.InvariantCulture);
}

/// <summary>CAT-R16 renditions: WebP at 320 / 640 / 1280 px wide under the image's key prefix.</summary>
public static class ImageRenditions
{
    public const int Small = 320;
    public const int Medium = 640;
    public const int Large = 1280;
    public const string OriginalName = "original";
    public const string WebPContentType = "image/webp";

    public static IReadOnlyList<int> Widths { get; } = [Small, Medium, Large];

    public static string Key(string prefix, int width) => string.Create(CultureInfo.InvariantCulture, $"{prefix}/{width}.webp");

    public static string OriginalKey(string prefix) => $"{prefix}/{OriginalName}";
}

/// <summary>JPEG, PNG or WebP recognised by magic bytes, never by file name (CAT-R16).</summary>
public static class ImageFormats
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MinShortestSide = 800;

    /// <summary>SEC-5: longest side the header may claim; checked before any pixel memory is allocated.</summary>
    public const int MaxSide = 8000;

    /// <summary>SEC-5: most pixels the header may claim (40 MP, about 160 MB decoded at 4 bytes per pixel).</summary>
    public const long MaxPixels = 40_000_000;

    /// <summary>The content type for the bytes' signature, or null when they are not JPEG, PNG or WebP.</summary>
    public static string? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8 && bytes[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        return bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8) ? "image/webp" : null;
    }
}

public sealed record ImageRendition(int Width, int Height, byte[] WebP);

/// <summary>Decodes an upload and re-encodes it to WebP renditions (EXIF dropped); no upscaling.</summary>
public interface IImageProcessor
{
    /// <summary>Renditions in <see cref="ImageRenditions.Widths"/> order; throws <see cref="RefusalException"/> (IMAGE_INVALID) when the bytes cannot be decoded, are too small, or claim more than <see cref="ImageFormats.MaxSide"/> px a side or <see cref="ImageFormats.MaxPixels"/> pixels.</summary>
    IReadOnlyList<ImageRendition> CreateRenditions(byte[] original);
}

/// <summary>UploadImage (CAT-R16, CAT-R17): store the files first, then commit the row; delete the files if the commit fails.</summary>
public sealed class ProductImageService(ProductService productService, IProductRepository products, IImageProcessor processor, IFileStorage storage, IUnitOfWork unitOfWork)
{
    public async Task<(AdminImageResponse Image, uint ProductVersion)> UploadAsync(Guid productId, Stream content, string? altText, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var product = await productService.LoadAsync(productId, cancellationToken);
        if (product.Status == ProductStatus.Archived)
        {
            throw new DomainException(CatalogErrors.InvalidStatusTransition, "An archived product cannot be changed; restore it first.");
        }

        if (string.IsNullOrWhiteSpace(altText))
        {
            throw new RefusalException(new FieldError("altText", CatalogErrors.ImageInvalid, "Enter alt text for the image."));
        }

        if (product.Images.Count >= Product.MaxImages)
        {
            throw new RefusalException(new FieldError("file", CatalogErrors.ImageInvalid, $"A product has at most {Product.MaxImages} images; remove one first."));
        }

        var bytes = await ReadLimitedAsync(content, cancellationToken);
        var contentType = ImageFormats.Detect(bytes)
            ?? throw new RefusalException(new FieldError("file", CatalogErrors.ImageInvalid, "Upload a JPEG, PNG or WebP image."));
        var renditions = processor.CreateRenditions(bytes);

        var imageId = IdGenerator.NewId();
        var prefix = ProductImage.KeyPrefix(productId, imageId);
        try
        {
            using (var original = new MemoryStream(bytes, writable: false))
            {
                await storage.SaveAsync(ImageRenditions.OriginalKey(prefix), original, contentType, cancellationToken);
            }

            for (var i = 0; i < renditions.Count; i++)
            {
                using var webp = new MemoryStream(renditions[i].WebP, writable: false);
                await storage.SaveAsync(ImageRenditions.Key(prefix, ImageRenditions.Widths[i]), webp, ImageRenditions.WebPContentType, cancellationToken);
            }

            var largest = renditions[^1];
            var image = product.AddImage(imageId, altText, largest.Width, largest.Height);
            products.MarkChanged(product);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            var response = new AdminImageResponse(
                image.Id,
                image.StorageKey,
                storage.GetPublicUrl(ImageRenditions.Key(prefix, ImageRenditions.Medium)),
                image.AltText,
                image.Width,
                image.Height,
                image.SortOrder);
            return (response, product.Version);
        }
        catch
        {
            await storage.DeletePrefixAsync(prefix, CancellationToken.None);
            throw;
        }
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > ImageFormats.MaxBytes)
            {
                throw new RefusalException(new FieldError("file", CatalogErrors.ImageInvalid, "Upload an image of at most 5 MB."));
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}

/// <summary>Follow-on of RemoveImage: deletes the image's files (CAT-R17). Idempotent.</summary>
public sealed class ProductImageRemovedHandler(IFileStorage storage) : IEventHandler<ProductImageRemoved>
{
    public Task HandleAsync(ProductImageRemoved domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return storage.DeletePrefixAsync(domainEvent.StorageKey, cancellationToken);
    }
}
