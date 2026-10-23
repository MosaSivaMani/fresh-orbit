using System.Net;
using AwesomeAssertions;
using FreshOrbit.Catalog.Tests.Integration;
using FreshOrbit.Catalog.Tests.Support;

[assembly: AssemblyFixture(typeof(CatalogDatabase))]

namespace FreshOrbit.Catalog.Tests.Integration;

public sealed class CatalogDatabase : StorefrontDatabase;

/// <summary>catalog.md §6 I1–I4: visibility at a location, search, location context.</summary>
public sealed class StorefrontTests(CatalogDatabase database) : StorefrontTestBase(database)
{
    [Fact]
    public async Task List_at_a_location_shows_only_priced_stocked_active_products_and_out_of_stock_last()
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var sellable = await CreatePublishedProductAsync(category, "Seer Fish");
        var unpriced = await CreatePublishedProductAsync(category, "Pomfret");
        var unstocked = await CreatePublishedProductAsync(category, "Mackerel");
        var empty = await CreatePublishedProductAsync(category, "Anchovy");
        var archived = await CreatePublishedProductAsync(category, "Sardine");

        await PriceAsync(served.LocationId, sellable.VariantId, 129900, 149900);
        await AddStockAsync(served.LocationId, sellable.VariantId, 20000);
        await AddStockAsync(served.LocationId, unpriced.VariantId, 20000);
        await PriceAsync(served.LocationId, unstocked.VariantId, 49900);
        await PriceAsync(served.LocationId, empty.VariantId, 39900);
        await AddStockAsync(served.LocationId, empty.VariantId, 0);
        await PriceAsync(served.LocationId, archived.VariantId, 29900);
        await AddStockAsync(served.LocationId, archived.VariantId, 20000);
        await OkAsync(HttpMethod.Post, $"/api/v1/admin/products/{archived.ProductId}/archive", null, archived.Version);

        var list = await GetPublicAsync($"/api/v1/products?pincode={served.Pincode}");

        var items = list.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("name").GetString()).Should().Equal("Seer Fish", "Anchovy");
        list.GetProperty("totalCount").GetInt32().Should().Be(2);
        items[0].GetProperty("fromPrice").GetProperty("unitPricePaise").GetInt64().Should().Be(129900);
        items[0].GetProperty("fromPrice").GetProperty("compareAtPaise").GetInt64().Should().Be(149900);
        items[0].GetProperty("fromPrice").GetProperty("perUnit").GetString().Should().Be("KG");
        items[0].GetProperty("availability").GetProperty("status").GetString().Should().Be("IN_STOCK");
        items[1].GetProperty("availability").GetProperty("status").GetString().Should().Be("OUT_OF_STOCK");
        items[0].GetProperty("locationRequired").GetBoolean().Should().BeFalse();

        var detail = await GetPublicAsync($"/api/v1/products/{unpriced.Slug}?pincode={served.Pincode}");
        detail.GetProperty("availability").GetProperty("status").GetString().Should().Be("NOT_SOLD_HERE");

        var sold = await GetPublicAsync($"/api/v1/products/{sellable.Slug}?pincode={served.Pincode}&preparation=CURRY_CUT");
        var variant = sold.GetProperty("variants")[0];
        variant.GetProperty("unitPricePaise").GetInt64().Should().Be(129900);
        variant.GetProperty("availability").GetProperty("maxOrderableNow").GetProperty("value").GetInt32().Should().Be(5000);
        variant.GetProperty("availability").GetProperty("maxOrderableNow").GetProperty("unit").GetString().Should().Be("GRAM");
        variant.GetProperty("preparations").EnumerateArray().Select(p => p.GetProperty("chargePaise").GetInt64()).Should().AllBeEquivalentTo(2000L);
        sold.GetProperty("location").GetProperty("fulfillmentLocationCode").GetString().Should().Be(served.LocationCode);

        using var client = Factory.CreateClient();
        var archivedDetail = await client.GetAsync($"/api/v1/products/{archived.Slug}", Ct);
        archivedDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Without_a_pincode_prices_are_null_and_location_is_required_for_price_filters()
    {
        var category = await CreateCategoryAsync();
        var product = await CreatePublishedProductAsync(category, "Seer Fish");

        var list = await GetPublicAsync($"/api/v1/products?category={(await OkAsync(HttpMethod.Get, $"/api/v1/admin/categories/{category}")).GetProperty("slug").GetString()}");
        var item = list.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("id").GetGuid().Should().Be(product.ProductId);
        item.GetProperty("fromPrice").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        item.GetProperty("locationRequired").GetBoolean().Should().BeTrue();

        using var client = Factory.CreateClient();
        var priceFilter = await client.GetAsync("/api/v1/products?minPricePaise=1000", Ct);
        priceFilter.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOfAsync(priceFilter)).Should().Be("LOCATION_REQUIRED");

        var availability = await client.GetAsync($"/api/v1/availability?variantId={product.VariantId}", Ct);
        (await CodeOfAsync(availability)).Should().Be("LOCATION_REQUIRED");

        var unknown = await client.GetAsync("/api/v1/products?pincode=999999", Ct);
        unknown.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOfAsync(unknown)).Should().Be("PINCODE_NOT_SERVICEABLE");
    }

    [Theory]
    [InlineData("vanjaram", "Seer Fish")]
    [InlineData("seer", "Seer Fish")]
    [InlineData("vanjiram", "Seer Fish")]
    [InlineData("prawn", "Tiger Prawns")]
    public async Task Search_matches_names_local_names_typos_and_prefixes(string q, string expected)
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var seer = await CreatePublishedProductAsync(category, "Seer Fish", ["Vanjaram", "Neymeen", "Surmai"], description: "Firm, mildly sweet flesh.");
        var prawns = await CreatePublishedProductAsync(category, "Tiger Prawns", ["Eral", "Konju"]);
        foreach (var product in new[] { seer, prawns })
        {
            await PriceAsync(served.LocationId, product.VariantId, 89900);
            await AddStockAsync(served.LocationId, product.VariantId, 10000);
        }

        var list = await GetPublicAsync($"/api/v1/products?q={q}", served.Pincode);

        list.GetProperty("items")[0].GetProperty("name").GetString().Should().Be(expected);

        var suggest = await GetPublicAsync($"/api/v1/search/suggest?q={q}", served.Pincode);
        suggest[0].GetProperty("name").GetString().Should().Be(expected);
    }

    [Fact]
    public async Task Availability_reports_each_variant_and_echoes_the_location()
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var product = await CreatePublishedProductAsync(category, "Seer Fish");
        await PriceAsync(served.LocationId, product.VariantId, 129900);
        await AddStockAsync(served.LocationId, product.VariantId, 1000, lowStockThreshold: 2000);
        var unknown = Guid.CreateVersion7();

        using var client = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/availability?variantId={product.VariantId}&variantId={unknown}&preparation=CURRY_CUT");
        request.Headers.Add("X-Pincode", served.Pincode);
        var response = await client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Fulfillment-Location").Should().Equal(served.LocationCode);
        var items = (await ReadJsonAsync(response)).GetProperty("items");
        items[0].GetProperty("status").GetString().Should().Be("LOW_STOCK");
        items[0].GetProperty("maxOrderableNow").GetProperty("value").GetInt32().Should().Be(500);
        items[1].GetProperty("status").GetString().Should().Be("NOT_SOLD_HERE");
    }

    [Fact]
    public async Task Home_lists_featured_products_and_categories()
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync("Home Fish");
        var featured = await CreatePublishedProductAsync(category, "Seer Fish", featuredRank: 1);
        await PriceAsync(served.LocationId, featured.VariantId, 129900);
        await AddStockAsync(served.LocationId, featured.VariantId, 10000);

        var home = await GetPublicAsync("/api/v1/home", served.Pincode);

        home.GetProperty("featuredProducts").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).Should().Contain(featured.ProductId);
        home.GetProperty("featuredCategories").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).Should().Contain(category);
        home.GetProperty("banners").GetArrayLength().Should().Be(0);
    }
}
