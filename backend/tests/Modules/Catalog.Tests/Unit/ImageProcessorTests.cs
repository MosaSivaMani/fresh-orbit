using AwesomeAssertions;
using FreshOrbit.Catalog.Infrastructure.Images;
using FreshOrbit.Catalog.Tests.Support;
using FreshOrbit.SharedKernel.Errors;
using SkiaSharp;

namespace FreshOrbit.Catalog.Tests.Unit;

/// <summary>CAT-R16 and SEC-5: dimensions are checked from the header before any pixel memory is allocated.</summary>
public sealed class ImageProcessorTests
{
    private readonly SkiaImageProcessor processor = new();

    [Theory]
    [InlineData(30_000, 30_000)]
    [InlineData(8_001, 1_000)]
    [InlineData(1_000, 8_001)]
    [InlineData(7_000, 7_000)]
    public void Header_claiming_too_many_pixels_is_refused_before_decoding(int width, int height)
    {
        var bomb = PngHeader.Claiming(Encode(900, 900, SKEncodedImageFormat.Png), width, height);
        bomb.Length.Should().BeLessThan(64 * 1024);

        var act = () => processor.CreateRenditions(bomb);

        act.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "file" && e.Code == "IMAGE_INVALID");
    }

    [Fact]
    public void Normal_png_still_gives_three_renditions()
    {
        var renditions = processor.CreateRenditions(Encode(1600, 1200, SKEncodedImageFormat.Png));

        renditions.Select(r => (r.Width, r.Height)).Should().Equal((320, 240), (640, 480), (1280, 960));
    }

    [Fact]
    public void Large_jpeg_is_decoded_at_a_reduced_scale_and_still_covers_the_largest_rendition()
    {
        var renditions = processor.CreateRenditions(Encode(4000, 3000, SKEncodedImageFormat.Jpeg));

        renditions.Select(r => (r.Width, r.Height)).Should().Equal((320, 240), (640, 480), (1280, 960));
    }

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(30, 120, 200));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
