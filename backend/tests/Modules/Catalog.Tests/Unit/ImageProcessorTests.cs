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

    [Theory]
    [InlineData(30_000, 30_000)]
    [InlineData(8_001, 500)]
    public void Sanitizer_refuses_a_bomb_header_like_product_uploads(int width, int height)
    {
        var bomb = PngHeader.Claiming(Encode(400, 400, SKEncodedImageFormat.Png), width, height);

        var act = () => processor.ToWebP(bomb, 320, 1280);

        act.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "file" && e.Code == "IMAGE_INVALID");
    }

    [Fact]
    public void Sanitizer_scales_down_to_one_webp_without_metadata()
    {
        var image = processor.ToWebP(Encode(2000, 1000, SKEncodedImageFormat.Jpeg), 320, 1280);

        (image.Width, image.Height).Should().Be((1280, 640));
        image.WebP.AsSpan(0, 4).SequenceEqual("RIFF"u8).Should().BeTrue();
        image.WebP.AsSpan(8, 4).SequenceEqual("WEBP"u8).Should().BeTrue();
        image.WebP.AsSpan().IndexOf("EXIF"u8).Should().Be(-1);
        image.WebP.AsSpan().IndexOf("XMP "u8).Should().Be(-1);
    }

    [Fact]
    public void Sanitizer_never_upscales_and_honours_the_callers_minimum_side()
    {
        processor.ToWebP(Encode(400, 300, SKEncodedImageFormat.Png), 300, 1280).Width.Should().Be(400);

        var tooSmall = () => processor.ToWebP(Encode(400, 299, SKEncodedImageFormat.Png), 300, 1280);
        tooSmall.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Code == "IMAGE_INVALID");
    }

    [Fact]
    public void Sanitizer_refuses_bytes_that_are_not_jpeg_png_or_webp()
    {
        var gif = "GIF89a-not-a-supported-image"u8.ToArray();

        var act = () => processor.ToWebP(gif, 1, 1280);

        act.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "file" && e.Code == "IMAGE_INVALID");
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
