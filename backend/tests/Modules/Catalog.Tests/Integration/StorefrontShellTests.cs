using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Catalog.Tests.Support;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Settings.Domain.BusinessProfiles;

namespace FreshOrbit.Catalog.Tests.Integration;

/// <summary>
/// Storefront shell fields the clients render without computing (W4, W5, W7, W11): cart line order rules, priced
/// wishlist cards, banner image URLs and the public business profile. The only test of this database that touches the
/// business profile.
/// </summary>
public sealed class StorefrontShellTests(CatalogDatabase database) : StorefrontTestBase(database)
{
    private static readonly string[] CodOnly = ["COD"];

    [Fact]
    public async Task Cart_line_carries_the_variant_order_rules()
    {
        var served = await CreateServedLocationAsync();
        await CreateOrderPolicyAsync(served.CityId);
        var category = await CreateCategoryAsync();
        var product = await CreatePublishedProductAsync(category, "Seer Fish");
        await PriceAsync(served.LocationId, product.VariantId, 129900);
        await AddStockAsync(served.LocationId, product.VariantId, 20000);
        using var guest = Factory.CreateClient();

        var response = await guest.PostAsJsonAsync(
            $"/api/v1/cart/items?pincode={served.Pincode}",
            new { variantId = product.VariantId, preparationCode = (string?)null, quantity = new { value = 500, unit = "GRAM" } },
            Ct);

        var cart = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(HttpStatusCode.OK, cart.ToString());
        var line = cart.GetProperty("lines").EnumerateArray().Single();
        line.GetProperty("variantId").GetGuid().Should().Be(product.VariantId);
        Quantity(line.GetProperty("minOrder")).Should().Be((250, "GRAM"));
        Quantity(line.GetProperty("maxOrder")).Should().Be((5000, "GRAM"));
        Quantity(line.GetProperty("orderStep")).Should().Be((250, "GRAM"));
    }

    [Fact]
    public async Task Wishlist_items_carry_a_server_priced_card_and_hidden_products_have_none()
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var sold = await CreatePublishedProductAsync(category, "Seer Fish");
        await PriceAsync(served.LocationId, sold.VariantId, 129900, 149900);
        await AddStockAsync(served.LocationId, sold.VariantId, 20000);
        var draft = (await CreateProductAsync(category, "Pomfret")).GetProperty("id").GetGuid();
        using var customer = await CreateCustomerClientAsync();
        (await customer.PutAsync($"/api/v1/me/wishlist/{sold.ProductId}", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await customer.PutAsync($"/api/v1/me/wishlist/{draft}", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var located = await ReadJsonAsync(await customer.GetAsync($"/api/v1/me/wishlist?pincode={served.Pincode}", Ct));
        var unlocated = await ReadJsonAsync(await customer.GetAsync("/api/v1/me/wishlist", Ct));
        var unknown = await customer.GetAsync("/api/v1/me/wishlist?pincode=999999", Ct);

        var card = Item(located, sold.ProductId).GetProperty("product");
        card.GetProperty("slug").GetString().Should().Be(sold.Slug);
        card.GetProperty("name").GetString().Should().Be("Seer Fish");
        card.GetProperty("imageUrl").GetString().Should().NotBeNullOrEmpty();
        card.GetProperty("isAvailable").GetBoolean().Should().BeTrue();
        card.GetProperty("price").GetProperty("unitPricePaise").GetInt64().Should().Be(129900);
        card.GetProperty("price").GetProperty("compareAtPaise").GetInt64().Should().Be(149900);
        card.GetProperty("price").GetProperty("perUnit").GetString().Should().Be("KG");
        Item(located, draft).GetProperty("product").ValueKind.Should().Be(JsonValueKind.Null);

        var unpriced = Item(unlocated, sold.ProductId).GetProperty("product");
        unpriced.GetProperty("slug").GetString().Should().Be(sold.Slug);
        unpriced.GetProperty("price").ValueKind.Should().Be(JsonValueKind.Null);
        unpriced.GetProperty("isAvailable").GetBoolean().Should().BeFalse();
        Item(unlocated, draft).GetProperty("product").ValueKind.Should().Be(JsonValueKind.Null);

        unknown.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOfAsync(unknown)).Should().Be("PINCODE_NOT_SERVICEABLE");
    }

    [Fact]
    public async Task Banners_carry_the_public_image_url_on_promotions_and_home()
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var product = await CreatePublishedProductAsync(category, "Seer Fish");
        var imageKey = $"banners/{Guid.NewGuid():N}.webp";
        var banner = await OkAsync(HttpMethod.Post, "/api/v1/admin/promotions", new
        {
            title = "Seer week",
            subtitle = "Fresh from Kasimedu.",
            imageKey,
            linkType = "PRODUCT",
            linkTarget = product.ProductId.ToString(),
            cityId = served.CityId,
            startsAt = Factory.Clock.UtcNow.AddHours(-1),
            endsAt = (DateTimeOffset?)null,
            sortOrder = 10,
            isActive = true,
        });
        var bannerId = banner.GetProperty("id").GetGuid();

        var promotions = await GetPublicAsync($"/api/v1/promotions?pincode={served.Pincode}");
        var home = await GetPublicAsync("/api/v1/home", served.Pincode);

        var listed = promotions.GetProperty("items").EnumerateArray().Single(b => b.GetProperty("id").GetGuid() == bannerId);
        listed.GetProperty("imageKey").GetString().Should().Be(imageKey);
        listed.GetProperty("imageUrl").GetString().Should().EndWith("/" + imageKey);
        var onHome = home.GetProperty("banners").EnumerateArray().Single(b => b.GetProperty("id").GetGuid() == bannerId);
        onHome.GetProperty("imageUrl").GetString().Should().Be(listed.GetProperty("imageUrl").GetString());
        onHome.GetProperty("title").GetString().Should().Be("Seer week");
        onHome.GetProperty("linkType").GetString().Should().Be("PRODUCT");
        onHome.GetProperty("linkTarget").GetString().Should().Be(product.ProductId.ToString());
    }

    [Fact]
    public async Task Public_business_profile_is_refused_until_configured_then_shows_support_hours()
    {
        using var anonymous = Factory.CreateClient();
        var missing = await anonymous.GetAsync("/api/v1/business-profile", Ct);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeOfAsync(missing)).Should().Be("BUSINESS_PROFILE_NOT_CONFIGURED");

        var first14 = "33AAACF1234A1Z";
        var gstin = first14 + Gstins.CheckCharacter(first14);
        using var settings = Factory.CreateStaffClient(["settings.manage"]);
        var saved = await SendAsync(settings, HttpMethod.Put, "/api/v1/admin/settings/business-profile", new
        {
            legalName = "Fresh Orbit Private Limited",
            tradeName = "Fresh Orbit",
            gstin,
            fssaiLicenseNo = "12345678901234",
            address = "1 Anna Salai, Chennai 600002",
            supportPhone = "+914412345678",
            supportEmail = "support@freshorbit.in",
            supportHours = "7 am - 9 pm, all days",
            timeZone = "Asia/Kolkata",
        }, ifNoneMatchAny: true);
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(Ct));
        (await ReadJsonAsync(saved)).GetProperty("supportHours").GetString().Should().Be("7 am - 9 pm, all days");

        var profile = await GetPublicAsync("/api/v1/business-profile");

        profile.GetProperty("legalName").GetString().Should().Be("Fresh Orbit Private Limited");
        profile.GetProperty("tradeName").GetString().Should().Be("Fresh Orbit");
        profile.GetProperty("gstin").GetString().Should().Be(gstin);
        profile.GetProperty("fssaiLicenseNo").GetString().Should().Be("12345678901234");
        profile.GetProperty("supportPhone").GetString().Should().Be("+914412345678");
        profile.GetProperty("supportEmail").GetString().Should().Be("support@freshorbit.in");
        profile.GetProperty("supportHours").GetString().Should().Be("7 am - 9 pm, all days");
        profile.GetProperty("timeZone").GetString().Should().Be("Asia/Kolkata");
    }

    private static (int Value, string? Unit) Quantity(JsonElement quantity)
        => (quantity.GetProperty("value").GetInt32(), quantity.GetProperty("unit").GetString());

    private static JsonElement Item(JsonElement wishlist, Guid productId)
        => wishlist.EnumerateArray().Single(i => i.GetProperty("productId").GetGuid() == productId);

    /// <summary>The cart prices lines with the city's order policy; this database has no default one.</summary>
    private async Task CreateOrderPolicyAsync(Guid cityId)
    {
        using var settings = Factory.CreateStaffClient(["settings.manage"]);
        var response = await SendAsync(settings, HttpMethod.Put, $"/api/v1/admin/settings/order-policies/{cityId}", new
        {
            reservationMinutes = 15,
            maxOverweightBp = 1000,
            cancelAllowedUntilStatus = "CONFIRMED",
            codMaxOrderPaise = 500000,
            enabledPaymentMethods = CodOnly,
            substitutionResponseMinutes = 15,
            complaintWindowHours = 24,
            maxDeliveryAttempts = 3,
        }, ifNoneMatchAny: true);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>A registered customer (row saved directly; no sign-in in this database) and a client acting as them.</summary>
    private async Task<HttpClient> CreateCustomerClientAsync()
    {
        var userId = Guid.CreateVersion7();
        var customer = Customer.Register(userId);
        await using (var db = Database.CreateDbContext())
        {
            db.Add(customer);
            await db.SaveChangesAsync(Ct);
        }

        return Factory.CreateCustomerClient(userId, customer.Id);
    }
}
