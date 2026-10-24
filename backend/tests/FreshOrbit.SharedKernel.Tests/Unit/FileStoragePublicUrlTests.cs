using AwesomeAssertions;
using FreshOrbit.SharedKernel.Web.Storage;

namespace FreshOrbit.SharedKernel.Tests.Unit;

/// <summary>W15: public image URLs are absolute on a server, relative paths only off Production.</summary>
public sealed class FileStoragePublicUrlTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "freshorbit-storage-url-tests");

    [Theory]
    [InlineData("/media")]
    [InlineData("http://api.example.test/media")]
    [InlineData("api.example.test/media")]
    [InlineData("")]
    public void Production_refuses_a_public_base_url_that_is_not_absolute_https(string publicBaseUrl)
    {
        var create = () => FileStorageExtensions.CreateLocalDisk(Root, publicBaseUrl, "Production");

        create.Should().Throw<InvalidOperationException>().WithMessage("*FileStorage:PublicBaseUrl*");
    }

    [Fact]
    public void Production_builds_absolute_urls_from_an_https_base()
    {
        var storage = FileStorageExtensions.CreateLocalDisk(Root, "https://api.example.test/media/", "Production");

        storage.GetPublicUrl("banners/abc.webp").Should().Be("https://api.example.test/media/banners/abc.webp");
    }

    [Fact]
    public void Development_keeps_the_relative_media_path()
    {
        var storage = FileStorageExtensions.CreateLocalDisk(Root);

        storage.GetPublicUrl("banners/abc.webp").Should().Be("/media/banners/abc.webp");
    }
}
