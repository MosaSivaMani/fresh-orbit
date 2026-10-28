using System.Globalization;

namespace FreshOrbit.Catalog.Infrastructure.Seeding;

/// <summary>One committed product picture of the sample set: its file, its number (1 = main picture) and its alt text.</summary>
public sealed record SampleProductPicture(int Number, string FilePath, string AltText);

/// <summary>
/// Real product pictures of the sample set (D249): <c>{sample directory}/images/products/{slug}/{n}.webp</c>, n = 1 to
/// <see cref="MaxPictures"/>, uploaded in number order so picture 1 is the main picture. Each file needs the alt text
/// of its number in <c>images.json</c>; a file without one is refused. A product with no file gets the placeholder.
/// </summary>
public static class SampleProductPictures
{
    /// <summary>Most committed pictures per sample product.</summary>
    public const int MaxPictures = 4;

    /// <summary>The image key whose committed file is picture <paramref name="number"/> of <paramref name="productSlug"/>.</summary>
    public static string ImageKey(string productSlug, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"sample/products/{productSlug}/{number}.webp");

    /// <summary>
    /// The committed pictures of <paramref name="productSlug"/> in number order; empty when there is none (the loader
    /// then uploads the placeholder). Throws <see cref="InvalidDataException"/> when a file has no alt text.
    /// </summary>
    public static IReadOnlyList<SampleProductPicture> Find(string? sampleDirectory, string productSlug, IReadOnlyDictionary<int, string> altTexts)
    {
        ArgumentNullException.ThrowIfNull(productSlug);
        ArgumentNullException.ThrowIfNull(altTexts);
        var pictures = new List<SampleProductPicture>();
        for (var number = 1; number <= MaxPictures; number++)
        {
            var file = SampleImageFiles.Find(sampleDirectory, ImageKey(productSlug, number));
            if (file is null)
            {
                continue;
            }

            if (!altTexts.TryGetValue(number, out var altText) || string.IsNullOrWhiteSpace(altText))
            {
                throw new InvalidDataException($"images.json: product {productSlug} has picture file {number}.webp but no altText for picture {number}.");
            }

            pictures.Add(new SampleProductPicture(number, file, altText.Trim()));
        }

        return pictures;
    }
}
