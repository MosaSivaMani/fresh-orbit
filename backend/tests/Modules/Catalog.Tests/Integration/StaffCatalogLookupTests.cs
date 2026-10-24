using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Catalog.Tests.Support;

namespace FreshOrbit.Catalog.Tests.Integration;

/// <summary>G3: staff-readable variant search and preparation methods (any staff token).</summary>
public sealed class StaffCatalogLookupTests(CatalogDatabase database) : StorefrontTestBase(database)
{
    [Fact]
    public async Task Variant_search_finds_by_product_name_variant_name_or_sku_for_staff_without_catalog_manage()
    {
        var marker = Guid.NewGuid().ToString("N")[..10];
        var product = await CreatePublishedProductAsync(await CreateCategoryAsync(), $"Seer {marker}");
        using var packer = Factory.CreateStaffClient(["orders.pack"], [Guid.CreateVersion7()]);

        (await packer.GetAsync("/api/v1/admin/products", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "product admin stays catalog.manage");
        var byName = await GetOkAsync(packer, $"/api/v1/admin/catalog/variants?q={marker.ToUpperInvariant()}");
        var bySku = await GetOkAsync(packer, $"/api/v1/admin/catalog/variants?q={product.Sku.ToLowerInvariant()}");

        var item = byName.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("variantId").GetGuid().Should().Be(product.VariantId);
        item.GetProperty("productId").GetGuid().Should().Be(product.ProductId);
        item.GetProperty("productName").GetString().Should().Be($"Seer {marker}");
        item.GetProperty("variantName").GetString().Should().NotBeNullOrEmpty();
        item.GetProperty("sku").GetString().Should().Be(product.Sku);
        item.GetProperty("sellUnit").GetString().Should().Be("KG");
        item.GetProperty("stockUnit").GetString().Should().Be("GRAM");
        item.GetProperty("productStatus").GetString().Should().Be("ACTIVE");
        item.GetProperty("isActive").GetBoolean().Should().BeTrue();
        byName.GetProperty("totalCount").GetInt32().Should().Be(1);
        bySku.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("variantId").GetGuid()).Should().Contain(product.VariantId);
    }

    [Fact]
    public async Task Variant_search_resolves_known_ids()
    {
        var first = await CreatePublishedProductAsync(await CreateCategoryAsync(), "Tiger Prawns");
        var second = await CreatePublishedProductAsync(await CreateCategoryAsync(), "Mud Crab");
        using var staff = Factory.CreateStaffClient(["inventory.view"], allLocations: true);

        var page = await GetOkAsync(staff, $"/api/v1/admin/catalog/variants?id={first.VariantId}&id={second.VariantId}&pageSize=100");

        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("variantId").GetGuid()).Should().BeEquivalentTo([first.VariantId, second.VariantId]);
    }

    [Fact]
    public async Task Variant_search_refuses_bad_paging_and_non_staff()
    {
        using var staff = Factory.CreateStaffClient(["orders.view"]);
        using var customer = Factory.CreateCustomerClient();
        using var anonymous = Factory.CreateClient();

        (await staff.GetAsync("/api/v1/admin/catalog/variants?pageSize=500", Ct)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var tooLong = await staff.GetAsync($"/api/v1/admin/catalog/variants?q={new string('a', 101)}", Ct);
        tooLong.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOfAsync(tooLong)).Should().Be("VALIDATION_FAILED");
        (await customer.GetAsync("/api/v1/admin/catalog/variants", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.GetAsync("/api/v1/admin/catalog/variants", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await customer.GetAsync("/api/v1/admin/preparation-methods", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Preparation_methods_are_readable_by_any_staff()
    {
        using var packer = Factory.CreateStaffClient(["orders.pack"]);

        var methods = await GetOkAsync(packer, "/api/v1/admin/preparation-methods");

        methods.EnumerateArray().Select(m => m.GetProperty("code").GetString()).Should().Contain("WHOLE");
    }

    private static async Task<JsonElement> GetOkAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        var json = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url} answered {json}");
        return json;
    }
}
