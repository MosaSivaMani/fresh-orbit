using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.Catalog.Application.Admin;
using FreshOrbit.Catalog.Domain.Preparations;
using FreshOrbit.Catalog.Domain.Products.Events;
using FreshOrbit.Catalog.Infrastructure.Seeding;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Catalog.Tests.Support;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Catalog.Tests.Integration;

/// <summary>Admin catalog: images (CAT-R16/R17), concurrency, uniqueness, permissions, reference data, public API.</summary>
public sealed class AdminCatalogTests(CatalogDatabase database) : StorefrontTestBase(database)
{
    [Fact]
    public async Task Image_upload_stores_three_webp_sizes_and_the_original()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category, "Seer Fish");
        var productId = product.GetProperty("id").GetGuid();

        var response = await UploadImageAsync(productId, Png(1600, 1200));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var image = await ReadJsonAsync(response);
        var prefix = image.GetProperty("storageKey").GetString()!;
        prefix.Should().StartWith($"products/{productId}/");
        var directory = Path.Combine(Factory.MediaRoot, prefix.Replace('/', Path.DirectorySeparatorChar));
        Directory.GetFiles(directory).Select(Path.GetFileName).Should().BeEquivalentTo(["320.webp", "640.webp", "1280.webp", "original"]);
        image.GetProperty("width").GetInt32().Should().Be(1280);
        image.GetProperty("height").GetInt32().Should().Be(960);
        image.GetProperty("url").GetString().Should().Be($"/media/{prefix}/640.webp");
        File.ReadAllBytes(Path.Combine(directory, "640.webp")).Take(4).Should().Equal("RIFF"u8.ToArray());

        using var scope = Factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IEventHandler<ProductImageRemoved>>();
        await handler.HandleAsync(new ProductImageRemoved(productId, image.GetProperty("id").GetGuid(), prefix), Ct);
        Directory.Exists(directory).Should().BeFalse();
    }

    [Fact]
    public async Task Invalid_images_are_refused_and_nothing_is_stored()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category, "Seer Fish");
        var productId = product.GetProperty("id").GetGuid();

        var text = await UploadImageAsync(productId, "this is not an image at all"u8.ToArray(), "photo.jpg");
        var tooSmall = await UploadImageAsync(productId, Png(500, 500));
        var tooLarge = await UploadImageAsync(productId, [.. Png(900, 900), .. new byte[6 * 1024 * 1024]]);
        var bomb = await UploadImageAsync(productId, PngClaiming(30_000, 30_000));

        foreach (var response in new[] { text, tooSmall, tooLarge, bomb })
        {
            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CodeOfAsync(response)).Should().Be("IMAGE_INVALID");
        }

        Directory.Exists(Path.Combine(Factory.MediaRoot, "products", productId.ToString())).Should().BeFalse();
    }

    [Fact]
    public async Task Variant_update_needs_the_current_product_version()
    {
        var category = await CreateCategoryAsync();
        var product = await CreatePublishedProductAsync(category, "Seer Fish");
        var url = $"/api/v1/admin/products/{product.ProductId}/variants/{product.VariantId}";
        var body = KgVariant(product.Sku, "Large", ("WHOLE", 10000, true), ("STEAK", 7000, false));

        var missing = await SendAsync(Admin, HttpMethod.Put, url, body);
        missing.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

        var stale = await SendAsync(Admin, HttpMethod.Put, url, body, "1");
        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var updated = await OkAsync(HttpMethod.Put, url, body, product.Version);
        var preparations = updated.GetProperty("variants")[0].GetProperty("preparations").EnumerateArray().ToList();
        preparations.Should().Contain(p => p.GetProperty("code").GetString() == "STEAK" && p.GetProperty("isActive").GetBoolean());
        preparations.Should().ContainSingle(p => p.GetProperty("code").GetString() == "CURRY_CUT" && !p.GetProperty("isActive").GetBoolean());
        preparations.Should().ContainSingle(p => p.GetProperty("isDefault").GetBoolean() && p.GetProperty("code").GetString() == "WHOLE");
    }

    [Fact]
    public async Task Slug_and_sku_are_unique()
    {
        var category = await CreateCategoryAsync();
        var first = await CreateProductAsync(category, "Seer Fish");
        var second = await CreateProductAsync(category, "Seer Fish");

        var sameSlug = await Admin.PostAsJsonAsync("/api/v1/admin/products", new
        {
            categoryId = category,
            name = "Seer Fish",
            slug = first.GetProperty("slug").GetString(),
            sourceType = "WILD",
        }, Ct);
        sameSlug.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOfAsync(sameSlug)).Should().Be("SLUG_TAKEN");

        var sku = UniqueSku("SEER");
        await OkAsync(HttpMethod.Post, $"/api/v1/admin/products/{first.GetProperty("id").GetGuid()}/variants", KgVariant(sku), first.GetProperty("version").GetString());
        var sameSku = await SendAsync(Admin, HttpMethod.Post, $"/api/v1/admin/products/{second.GetProperty("id").GetGuid()}/variants", KgVariant(sku), second.GetProperty("version").GetString());
        sameSku.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOfAsync(sameSku)).Should().Be("SKU_TAKEN");
    }

    [Fact]
    public async Task Admin_catalog_needs_catalog_manage()
    {
        using var customer = Factory.CreateCustomerClient();
        using var staff = Factory.CreateStaffClient(["orders.view"], allLocations: true);
        using var anonymous = Factory.CreateClient();

        (await customer.GetAsync("/api/v1/admin/products", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.PostAsJsonAsync("/api/v1/admin/categories", new { name = "Fish" }, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.GetAsync("/api/v1/admin/preparation-methods", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Publish_without_image_lists_the_missing_items()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category, "Seer Fish");

        var response = await SendAsync(Admin, HttpMethod.Post, $"/api/v1/admin/products/{product.GetProperty("id").GetGuid()}/publish", null, product.GetProperty("version").GetString());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var errors = (await ReadJsonAsync(response)).GetProperty("errors");
        errors.TryGetProperty("images", out _).Should().BeTrue();
        errors.TryGetProperty("variants", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Preparation_methods_load_idempotently_and_the_public_api_reads_variants()
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var loader = scope.ServiceProvider.GetServices<IReferenceDataLoader>().OfType<PreparationMethodsLoader>().Single();
            await loader.LoadAsync(new ReferenceDataContext(StorefrontDatabase.ReferenceDataPath), Ct);
        }

        await using (var db = Database.CreateDbContext())
        {
            (await db.Set<PreparationMethod>().CountAsync(Ct)).Should().Be(16);
            (await db.Set<PreparationMethod>().SingleAsync(m => m.Code == "CURRY_CUT", Ct)).GuidanceYieldBp.Should().Be(6500);
        }

        var category = await CreateCategoryAsync();
        var product = await CreatePublishedProductAsync(category, "Seer Fish");
        await using var apiScope = Factory.Services.CreateAsyncScope();
        var api = apiScope.ServiceProvider.GetRequiredService<ICatalogApi>();
        var variants = await api.GetVariantsBySkusAsync([product.Sku.ToLowerInvariant()], Ct);
        var variant = variants.Should().ContainSingle().Subject;
        variant.ProductStatus.Should().Be(ProductStatus.Active);
        variant.SellUnit.Should().Be(SellUnit.Kg);
        variant.TaxCategoryCode.Should().Be(TaxCategory);
        variant.Preparations.Should().ContainSingle(p => p.IsDefault && p.Code == "CURRY_CUT" && p.YieldBp == 6500);
        (await api.GetSellableVariantIdsAsync(Ct)).Should().Contain(product.VariantId);
        (await api.GetProductSummariesAsync([product.ProductId], Ct)).Should().ContainSingle(s => s.Slug == product.Slug && s.ImageKey != null);
        ImageRenditions.Key(variant.ImageKey!, ImageRenditions.Small).Should().EndWith("/320.webp");
    }
}
