using System.Globalization;
using AwesomeAssertions;
using FreshOrbit.Catalog.Infrastructure.Seeding;

namespace FreshOrbit.Catalog.Tests.Unit;

/// <summary>D249: committed sample product pictures are images/products/{slug}/{n}.webp, n = 1..4, in number order.</summary>
public sealed class SampleProductPicturesTests : IDisposable
{
    private const string Slug = "seer-fish";

    private static readonly Dictionary<int, string> AltTexts = new()
    {
        [1] = "Seer fish, whole on crushed ice",
        [2] = "Seer fish steaks",
        [3] = "Andhra seer fish fry",
    };

    private readonly string _sample = Path.Combine(Path.GetTempPath(), "freshorbit-sample-pictures", Guid.NewGuid().ToString("N"));

    [Fact]
    public void No_committed_file_gives_no_picture_so_the_placeholder_is_used()
    {
        Directory.CreateDirectory(_sample);

        SampleProductPictures.Find(_sample, Slug, AltTexts).Should().BeEmpty();
    }

    [Fact]
    public void Committed_files_come_in_number_order_with_their_alt_text()
    {
        Commit(3);
        Commit(1);

        var pictures = SampleProductPictures.Find(_sample, Slug, AltTexts);

        pictures.Select(p => (p.Number, p.AltText)).Should().Equal((1, "Seer fish, whole on crushed ice"), (3, "Andhra seer fish fry"));
        pictures[0].FilePath.Should().Be(Path.GetFullPath(Path.Combine(_sample, "images", "products", Slug, "1.webp")));
    }

    [Fact]
    public void Files_of_another_product_or_another_name_are_ignored()
    {
        Commit(1, "rohu");
        Directory.CreateDirectory(Path.Combine(_sample, "images", "products", Slug));
        File.WriteAllBytes(Path.Combine(_sample, "images", "products", Slug, "1.png"), [1]);
        File.WriteAllBytes(Path.Combine(_sample, "images", "products", Slug, "5.webp"), [1]);

        SampleProductPictures.Find(_sample, Slug, AltTexts).Should().BeEmpty();
    }

    [Fact]
    public void A_file_without_alt_text_is_refused()
    {
        Commit(4);

        var act = () => SampleProductPictures.Find(_sample, Slug, AltTexts);

        act.Should().Throw<InvalidDataException>().WithMessage("*seer-fish*4*");
    }

    [Fact]
    public void No_sample_directory_gives_no_picture()
    {
        SampleProductPictures.Find(null, Slug, AltTexts).Should().BeEmpty();
    }

    [Fact]
    public void Image_key_is_the_committed_file_path_under_sample()
    {
        SampleProductPictures.ImageKey(Slug, 2).Should().Be("sample/products/seer-fish/2.webp");
    }

    public void Dispose()
    {
        if (Directory.Exists(_sample))
        {
            Directory.Delete(_sample, recursive: true);
        }
    }

    private void Commit(int number, string slug = Slug)
    {
        var directory = Path.Combine(_sample, "images", "products", slug);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, number.ToString(CultureInfo.InvariantCulture) + ".webp"), [0x52, 0x49, 0x46, 0x46]);
    }
}
