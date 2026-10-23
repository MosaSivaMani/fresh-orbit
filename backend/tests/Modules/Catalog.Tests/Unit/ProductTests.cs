using AwesomeAssertions;
using FreshOrbit.Catalog.Application.Admin;
using FreshOrbit.Catalog.Application.Storefront;
using FreshOrbit.Catalog.Domain.Categories;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.Catalog.Domain.Products.Events;
using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Catalog.Tests.Unit;

public sealed class ProductTests
{
    private static readonly Guid CurryCut = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 6, 0, 0, TimeSpan.Zero);

    private static ProductDetails Details(string? slug = "seer-fish", string? hsn = "0302", string? tax = "SEAFOOD_FRESH_CHILLED", IReadOnlyList<string>? localNames = null)
        => new(slug, "Seer Fish", localNames ?? ["Vanjaram", "vanjaram", "Neymeen"], null, null, SourceType.Wild, null, null, null, hsn, tax, null);

    private static VariantSpec Spec(bool defaultPreparation = true)
        => new("Medium", SellUnit.Kg, null, null, null, null, null, [new(CurryCut, "CURRY_CUT", 6500, defaultPreparation)]);

    private static string Code(Guid id) => id == CurryCut ? "CURRY_CUT" : "WHOLE";

    private static Product Publishable()
    {
        var product = Product.Create(Guid.CreateVersion7(), Details());
        product.AddVariant("SEER-KG", Spec(), skuTaken: false);
        product.AddImage(Guid.CreateVersion7(), "Seer fish steaks", 1280, 960);
        return product;
    }

    [Fact]
    public void Local_names_are_trimmed_and_deduplicated_case_insensitively()
        => Product.Create(Guid.CreateVersion7(), Details()).LocalNames.Should().Equal("Vanjaram", "Neymeen");

    [Fact]
    public void Slug_is_generated_from_the_name_when_absent()
        => Product.Create(Guid.CreateVersion7(), Details(slug: null)).Slug.Should().Be("seer-fish");

    [Fact]
    public void Publish_lists_every_missing_item()
    {
        var product = Product.Create(Guid.CreateVersion7(), Details(hsn: null, tax: null));

        var act = () => product.Publish(categoryActive: false, Code, Now);

        act.Should().Throw<RefusalException>().Which.Errors.Select(e => e.Field)
            .Should().BeEquivalentTo(["categoryId", "hsnCode", "taxCategoryCode", "images", "variants"]);
        product.Status.Should().Be(ProductStatus.Draft);
    }

    [Fact]
    public void Publish_activates_and_records_the_first_publish_time()
    {
        var product = Publishable();

        product.Publish(categoryActive: true, Code, Now);

        product.Status.Should().Be(ProductStatus.Active);
        product.PublishedAt.Should().Be(Now);
        product.DomainEvents.Should().ContainSingle(e => e is ProductPublished);
    }

    [Fact]
    public void Slug_cannot_change_after_publish()
    {
        var product = Publishable();
        product.Publish(true, Code, Now);

        var act = () => product.Update(product.CategoryId, Details(slug: "seer-fish-new"));

        act.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "slug");
    }

    [Fact]
    public void The_only_active_variant_of_an_active_product_cannot_be_deactivated()
    {
        var product = Publishable();
        product.Publish(true, Code, Now);

        var act = () => product.UpdateVariant(product.Variants[0].Id, Spec(), isActive: false);

        act.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "isActive");
    }

    [Fact]
    public void Archive_restore_and_refused_transitions()
    {
        var product = Publishable();
        product.Publish(true, Code, Now);

        product.Archive();
        product.Status.Should().Be(ProductStatus.Archived);
        product.Restore();
        product.Status.Should().Be(ProductStatus.Draft);

        var publishTwice = Publishable();
        publishTwice.Publish(true, Code, Now);
        var restoreActive = () => publishTwice.Restore();
        restoreActive.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.InvalidStatusTransition);
    }

    [Fact]
    public void Duplicate_sku_is_a_conflict()
    {
        var product = Product.Create(Guid.CreateVersion7(), Details());

        var act = () => product.AddVariant("SEER-KG", Spec(), skuTaken: true);

        act.Should().Throw<ConflictException>().Which.Code.Should().Be(CatalogErrors.SkuTaken);
    }

    [Fact]
    public void Images_are_limited_to_eight_and_removal_raises_the_follow_on_event()
    {
        var product = Product.Create(Guid.CreateVersion7(), Details());
        var ids = Enumerable.Range(0, Product.MaxImages).Select(_ => Guid.CreateVersion7()).ToList();
        foreach (var id in ids)
        {
            product.AddImage(id, "Seer fish", 1280, 960);
        }

        var ninth = () => product.AddImage(Guid.CreateVersion7(), "Seer fish", 1280, 960);
        ninth.Should().Throw<RefusalException>().Which.Code.Should().Be(CatalogErrors.ImageInvalid);

        product.RemoveImage(ids[0]);
        product.DomainEvents.OfType<ProductImageRemoved>().Should().ContainSingle(e => e.StorageKey == ProductImage.KeyPrefix(product.Id, ids[0]));
    }

    [Fact]
    public void Categories_have_at_most_two_levels()
    {
        var top = Category.Create(null, "fish", "Fish", null, 10);
        var child = Category.Create(top, "sea-fish", "Sea fish", null, 10);

        var third = () => Category.Create(child, "deep", "Deep sea", null, 10);
        third.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "parentId");

        var self = () => top.Update(top, false, null, "Fish", null, null, 10, true);
        self.Should().Throw<RefusalException>();
    }

    [Fact]
    public void Image_formats_are_recognised_by_magic_bytes()
    {
        ImageFormats.Detect([0xFF, 0xD8, 0xFF, 0xE0]).Should().Be("image/jpeg");
        ImageFormats.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]).Should().Be("image/png");
        ImageFormats.Detect("RIFF\0\0\0\0WEBPVP8 "u8).Should().Be("image/webp");
        ImageFormats.Detect("hello, I am text"u8).Should().BeNull();
    }

    [Fact]
    public void Storefront_query_rules()
    {
        var priceWithoutLocation = () => StorefrontService.BuildQuery(null, null, null, null, null, 1000, null, null, 0, 20, hasLocation: false);
        priceWithoutLocation.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.LocationRequired);

        var relevanceWithoutQuery = () => StorefrontService.BuildQuery(null, null, null, null, null, null, null, "relevance", 0, 20, hasLocation: true);
        relevanceWithoutQuery.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "sort");

        var shortQuery = () => StorefrontService.BuildQuery(null, "a", null, null, null, null, null, null, 0, 20, hasLocation: true);
        shortQuery.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "q");

        StorefrontService.BuildQuery(null, "seer", null, null, null, null, null, null, 0, 20, hasLocation: false).Sort.Should().Be(ProductSort.Relevance);
    }
}
