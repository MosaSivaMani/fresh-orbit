using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Catalog.Tests.Support;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Pricing;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Catalog.Tests.Integration;

/// <summary>
/// catalog.md §6 I10–I16: customer product shapes — default variant and order rules, compare-at discount, serving facts,
/// freshness, offer line, bestsellers, public preparation methods — and the cart's pre-add price quote (cart.md CART-R17).
/// </summary>
public sealed class StorefrontProductShapeTests(CatalogDatabase database) : StorefrontTestBase(database)
{
    private static readonly string[] CodOnly = ["COD"];

    /// <summary>Staff who can also receive stock, manage coupons and order policies.</summary>
    private HttpClient NewOperations() => Factory.CreateStaffClient(
        ["catalog.manage", "inventory.adjust", "inventory.receive", "inventory.view", "promotions.manage", "settings.manage"], allLocations: true);

    [Fact]
    public async Task List_items_carry_the_default_variant_rules_discount_and_serving_and_detail_matches()
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var product = await CreatePublishedProductAsync(category, "Seer Fish");
        await SetServingAsync(product, piecesMin: 10, piecesMax: 12, serves: 4);
        await PriceAsync(served.LocationId, product.VariantId, 129900, 149900);
        await AddStockAsync(served.LocationId, product.VariantId, 20000);

        var item = await ListItemAsync(category, served.Pincode, product.ProductId);

        item.GetProperty("fromPrice").GetProperty("discountBp").GetInt32().Should().Be(1334);
        var variant = item.GetProperty("defaultVariant");
        variant.GetProperty("id").GetGuid().Should().Be(product.VariantId);
        variant.GetProperty("sellUnit").GetString().Should().Be("KG");
        AssertQuantity(variant.GetProperty("minOrder"), 250, "GRAM");
        AssertQuantity(variant.GetProperty("maxOrder"), 5000, "GRAM");
        AssertQuantity(variant.GetProperty("orderStep"), 250, "GRAM");
        variant.GetProperty("defaultPreparationCode").GetString().Should().Be("CURRY_CUT");
        var serving = variant.GetProperty("serving");
        AssertQuantity(serving.GetProperty("basis"), 1000, "GRAM");
        serving.GetProperty("netWeightGrams").GetInt32().Should().Be(1000);
        serving.GetProperty("piecesMin").GetInt32().Should().Be(10);
        serving.GetProperty("piecesMax").GetInt32().Should().Be(12);
        serving.GetProperty("serves").GetInt32().Should().Be(4);
        item.GetProperty("freshness").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("offer").ValueKind.Should().Be(JsonValueKind.Null);
        item.TryGetProperty("categoryId", out _).Should().BeFalse();

        var detail = await GetPublicAsync($"/api/v1/products/{product.Slug}", served.Pincode);
        var detailVariant = detail.GetProperty("variants")[0];
        detailVariant.GetProperty("discountBp").GetInt32().Should().Be(1334);
        detailVariant.GetProperty("serving").GetProperty("serves").GetInt32().Should().Be(4);

        var withoutLocation = await ListItemAsync(category, null, product.ProductId);
        withoutLocation.GetProperty("defaultVariant").GetProperty("id").GetGuid().Should().Be(product.VariantId);
        withoutLocation.GetProperty("fromPrice").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task No_discount_is_shown_without_a_compare_at_price()
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var product = await CreatePublishedProductAsync(category, "Pomfret");
        await PriceAsync(served.LocationId, product.VariantId, 89900);
        await AddStockAsync(served.LocationId, product.VariantId, 20000);

        var item = await ListItemAsync(category, served.Pincode, product.ProductId);

        item.GetProperty("fromPrice").GetProperty("discountBp").ValueKind.Should().Be(JsonValueKind.Null);
        var serving = item.GetProperty("defaultVariant").GetProperty("serving");
        serving.GetProperty("piecesMin").ValueKind.Should().Be(JsonValueKind.Null);
        serving.GetProperty("serves").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Admin_variant_refuses_an_inverted_pieces_range_and_pieces_on_a_piece_variant()
    {
        var category = await CreateCategoryAsync();
        var product = await CreatePublishedProductAsync(category, "Mackerel");

        var inverted = await SendAsync(Admin, HttpMethod.Put, $"/api/v1/admin/products/{product.ProductId}/variants/{product.VariantId}", VariantBody(product.Sku, 12, 10, 4), product.Version);
        inverted.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadJsonAsync(inverted)).GetProperty("errors").TryGetProperty("piecesMax", out _).Should().BeTrue();

        var badServes = await SendAsync(Admin, HttpMethod.Put, $"/api/v1/admin/products/{product.ProductId}/variants/{product.VariantId}", VariantBody(product.Sku, null, null, 0), product.Version);
        (await ReadJsonAsync(badServes)).GetProperty("errors").TryGetProperty("serves", out _).Should().BeTrue();

        var piece = await SendAsync(Admin, HttpMethod.Post, $"/api/v1/admin/products/{product.ProductId}/variants", new
        {
            sku = UniqueSku("CRAB"),
            name = "Live crab",
            sellUnit = "PIECE",
            avgPieceGrams = 450,
            minOrder = 1,
            maxOrder = 10,
            orderStep = 1,
            piecesMin = 1,
            piecesMax = 1,
            preparations = new[] { new { code = "WHOLE", yieldBp = 10000, isDefault = true } },
        }, product.Version);
        piece.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadJsonAsync(piece)).GetProperty("errors").TryGetProperty("piecesMin", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Freshness_is_the_batch_the_next_order_is_packed_from()
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var product = await CreatePublishedProductAsync(category, "Sardine");
        await PriceAsync(served.LocationId, product.VariantId, 29900);
        using var operations = NewOperations();
        var created = await operations.PostAsJsonAsync("/api/v1/admin/inventory/stock-items", new { fulfillmentLocationId = served.LocationId, variantId = product.VariantId, lowStockThreshold = 1000 }, Ct);
        created.IsSuccessStatusCode.Should().BeTrue(await created.Content.ReadAsStringAsync(Ct));
        var now = Factory.Clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        await ReceiveAsync(served.LocationId, product.VariantId, today.AddDays(-1), now.AddDays(2));
        await ReceiveAsync(served.LocationId, product.VariantId, today, now.AddDays(3));

        var item = await ListItemAsync(category, served.Pincode, product.ProductId);
        var freshness = item.GetProperty("freshness");
        freshness.GetProperty("caughtOn").GetString().Should().Be(today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        freshness.GetProperty("receivedAt").GetDateTimeOffset().Should().NotBe(default);

        var detail = await GetPublicAsync($"/api/v1/products/{product.Slug}", served.Pincode);
        detail.GetProperty("variants")[0].GetProperty("freshness").GetProperty("caughtOn").GetString().Should().Be(today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var noLocation = await GetPublicAsync($"/api/v1/products/{product.Slug}");
        noLocation.GetProperty("variants")[0].GetProperty("freshness").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Offer_line_shows_the_most_specific_listed_coupon_of_the_city()
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var scoped = await CreatePublishedProductAsync(category, "Tiger Prawns");
        var other = await CreatePublishedProductAsync(category, "Squid");
        foreach (var p in new[] { scoped, other })
        {
            await PriceAsync(served.LocationId, p.VariantId, 59900);
            await AddStockAsync(served.LocationId, p.VariantId, 20000);
        }

        var cityWide = $"CITY{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var productOnly = $"PRWN{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var hidden = $"HIDE{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        await CreateCouponAsync(cityWide, served.CityId, "FLAT", 5000, null, 49900, listed: true, productId: null);
        await CreateCouponAsync(productOnly, served.CityId, "PERCENT", 1500, 10000, 0, listed: true, productId: scoped.ProductId);
        await CreateCouponAsync(hidden, served.CityId, "FLAT", 20000, null, 0, listed: false, productId: scoped.ProductId);

        var scopedItem = await ListItemAsync(category, served.Pincode, scoped.ProductId);
        scopedItem.GetProperty("offer").GetProperty("couponCode").GetString().Should().Be(productOnly);
        scopedItem.GetProperty("offer").GetProperty("text").GetString().Should().Contain(productOnly);

        var otherItem = await ListItemAsync(category, served.Pincode, other.ProductId);
        otherItem.GetProperty("offer").GetProperty("couponCode").GetString().Should().Be(cityWide);

        var detail = await GetPublicAsync($"/api/v1/products/{scoped.Slug}", served.Pincode);
        detail.GetProperty("offer").GetProperty("couponCode").GetString().Should().Be(productOnly);

        var withoutLocation = await ListItemAsync(category, null, scoped.ProductId);
        withoutLocation.GetProperty("offer").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Bestsellers_rank_real_sold_orders_at_the_location_and_are_empty_without_sales()
    {
        var served = await CreateServedLocationAsync();
        var quiet = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var top = await CreatePublishedProductAsync(category, "Seer Fish");
        var second = await CreatePublishedProductAsync(category, "Pomfret");
        var unpaidOnly = await CreatePublishedProductAsync(category, "Lobster");
        var unsold = await CreatePublishedProductAsync(category, "Anchovy");
        foreach (var p in new[] { top, second, unpaidOnly, unsold })
        {
            await PriceAsync(served.LocationId, p.VariantId, 49900);
            await AddStockAsync(served.LocationId, p.VariantId, 20000);
            await PriceAsync(quiet.LocationId, p.VariantId, 49900);
            await AddStockAsync(quiet.LocationId, p.VariantId, 20000);
        }

        await PlaceOrderAsync(served.LocationId, PaymentMethod.Cod, top, second);
        await PlaceOrderAsync(served.LocationId, PaymentMethod.Cod, top);
        await PlaceOrderAsync(served.LocationId, PaymentMethod.Online, unpaidOnly);
        await PlaceOrderAsync(Guid.CreateVersion7(), PaymentMethod.Cod, unsold);

        var bestsellers = await GetPublicAsync("/api/v1/bestsellers", served.Pincode);
        bestsellers.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Equal(top.ProductId, second.ProductId);
        bestsellers.GetProperty("location").GetProperty("fulfillmentLocationCode").GetString().Should().Be(served.LocationCode);
        bestsellers.GetProperty("items")[0].GetProperty("defaultVariant").GetProperty("id").GetGuid().Should().Be(top.VariantId);

        var home = await GetPublicAsync("/api/v1/home", served.Pincode);
        home.GetProperty("bestsellers").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Equal(top.ProductId, second.ProductId);

        var none = await GetPublicAsync("/api/v1/bestsellers", quiet.Pincode);
        none.GetProperty("items").GetArrayLength().Should().Be(0);

        var homeWithoutLocation = await GetPublicAsync("/api/v1/home");
        homeWithoutLocation.GetProperty("bestsellers").GetArrayLength().Should().Be(0);

        using var client = Factory.CreateClient();
        var required = await client.GetAsync("/api/v1/bestsellers", Ct);
        required.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOfAsync(required)).Should().Be("LOCATION_REQUIRED");
    }

    [Fact]
    public async Task Preparation_methods_list_only_methods_offered_on_visible_variants()
    {
        var served = await CreateServedLocationAsync();
        var category = await CreateCategoryAsync();
        var product = await CreatePublishedProductAsync(category, "Seer Fish");
        await PriceAsync(served.LocationId, product.VariantId, 129900, null, "CURRY_CUT");
        await AddStockAsync(served.LocationId, product.VariantId, 20000);

        var all = await GetPublicAsync("/api/v1/preparation-methods");
        var codes = all.EnumerateArray().Select(m => m.GetProperty("code").GetString()).ToList();
        codes.Should().Contain(["WHOLE", "CURRY_CUT"]);
        all.EnumerateArray().Select(m => m.GetProperty("sortOrder").GetInt32()).Should().BeInAscendingOrder();

        var here = await GetPublicAsync("/api/v1/preparation-methods", served.Pincode);
        here.EnumerateArray().Select(m => m.GetProperty("code").GetString()).Should().Equal("CURRY_CUT");
    }

    [Fact]
    public async Task Price_quote_matches_the_cart_line_formula_and_refuses_invalid_quantities()
    {
        var served = await CreateServedLocationAsync();
        await SavePolicyAsync(served.CityId, maxOverweightBp: 1000);
        var category = await CreateCategoryAsync();
        var product = await CreatePublishedProductAsync(category, "Seer Fish");
        await PriceAsync(served.LocationId, product.VariantId, 129900, 149900);
        await AddStockAsync(served.LocationId, product.VariantId, 20000);

        var quote = await GetPublicAsync($"/api/v1/price-quote?variantId={product.VariantId}&preparation=CURRY_CUT&quantity=750&unit=GRAM", served.Pincode);

        var facts = new LinePriceFacts(PricedUnit.Gram, 129900, 2000, PreparationChargeBasis.PerKg, 500);
        var estimated = LinePricing.Compute(facts, 750);
        var max = LinePricing.Compute(facts, LinePricing.MaxChargeableQuantity(750, true, 1000));
        quote.GetProperty("itemPaise").GetInt64().Should().Be(estimated.ItemPaise);
        quote.GetProperty("preparationPaise").GetInt64().Should().Be(estimated.PreparationPaise);
        quote.GetProperty("estimatedTotalPaise").GetInt64().Should().Be(estimated.TotalPaise);
        quote.GetProperty("maxChargeablePaise").GetInt64().Should().Be(max.TotalPaise);
        AssertQuantityView(quote.GetProperty("maxChargeableQuantity"), 825, "GRAM");
        quote.GetProperty("taxPaise").GetInt64().Should().Be(LinePricing.ContainedTax(estimated.TotalPaise, 500));
        quote.GetProperty("discountBp").GetInt32().Should().Be(1334);
        quote.GetProperty("preparationCode").GetString().Should().Be("CURRY_CUT");
        quote.GetProperty("availability").GetProperty("status").GetString().Should().Be("IN_STOCK");
        quote.GetProperty("weightNote").GetString().Should().NotBeNullOrEmpty();

        var defaulted = await GetPublicAsync($"/api/v1/price-quote?variantId={product.VariantId}&quantity=500&unit=GRAM&pincode={served.Pincode}");
        defaulted.GetProperty("preparationCode").GetString().Should().Be("CURRY_CUT");

        using var client = Factory.CreateClient();
        await ExpectRefusalAsync(client, $"/api/v1/price-quote?variantId={product.VariantId}&quantity=300&unit=GRAM&pincode={served.Pincode}", "QUANTITY_STEP_INVALID", "quantity");
        await ExpectRefusalAsync(client, $"/api/v1/price-quote?variantId={product.VariantId}&quantity=0&unit=GRAM&pincode={served.Pincode}", "QUANTITY_BELOW_MIN", "quantity");
        await ExpectRefusalAsync(client, $"/api/v1/price-quote?variantId={product.VariantId}&quantity=5250&unit=GRAM&pincode={served.Pincode}", "QUANTITY_ABOVE_MAX", "quantity");
        await ExpectRefusalAsync(client, $"/api/v1/price-quote?variantId={product.VariantId}&quantity=2&unit=PIECE&pincode={served.Pincode}", "QUANTITY_UNIT_INVALID", "unit");
        await ExpectRefusalAsync(client, $"/api/v1/price-quote?variantId={product.VariantId}&quantity=500&unit=GRAM&preparation=FILLET&pincode={served.Pincode}", "PREPARATION_NOT_AVAILABLE", "preparation");
        await ExpectRefusalAsync(client, $"/api/v1/price-quote?variantId={Guid.CreateVersion7()}&quantity=500&unit=GRAM&pincode={served.Pincode}", "PRODUCT_NOT_AVAILABLE", "variantId");
        await ExpectRefusalAsync(client, $"/api/v1/price-quote?variantId={product.VariantId}&unit=GRAM&pincode={served.Pincode}", "VALIDATION_FAILED", "quantity");

        var noPincode = await client.GetAsync($"/api/v1/price-quote?variantId={product.VariantId}&quantity=500&unit=GRAM", Ct);
        noPincode.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOfAsync(noPincode)).Should().Be("LOCATION_REQUIRED");

        var unpriced = await CreatePublishedProductAsync(category, "Pomfret");
        await ExpectRefusalAsync(client, $"/api/v1/price-quote?variantId={unpriced.VariantId}&quantity=500&unit=GRAM&pincode={served.Pincode}", "PRICE_NOT_AVAILABLE", "variantId");
    }

    private static void AssertQuantity(JsonElement quantity, int value, string unit)
    {
        quantity.GetProperty("value").GetInt32().Should().Be(value);
        quantity.GetProperty("unit").GetString().Should().Be(unit);
    }

    private static void AssertQuantityView(JsonElement quantity, int value, string unit) => AssertQuantity(quantity, value, unit);

    private static object VariantBody(string sku, int? piecesMin, int? piecesMax, int? serves) => new
    {
        sku,
        name = "Medium",
        sellUnit = "KG",
        minOrder = 250,
        maxOrder = 5000,
        orderStep = 250,
        isActive = true,
        piecesMin,
        piecesMax,
        serves,
        preparations = new[] { new { code = "WHOLE", yieldBp = 10000, isDefault = false }, new { code = "CURRY_CUT", yieldBp = 6500, isDefault = true } },
    };

    private async Task SetServingAsync(PublishedProduct product, int piecesMin, int piecesMax, int serves)
        => await OkAsync(HttpMethod.Put, $"/api/v1/admin/products/{product.ProductId}/variants/{product.VariantId}", VariantBody(product.Sku, piecesMin, piecesMax, serves), product.Version);

    private async Task<JsonElement> ListItemAsync(Guid categoryId, string? pincode, Guid productId)
    {
        var slug = (await OkAsync(HttpMethod.Get, $"/api/v1/admin/categories/{categoryId}")).GetProperty("slug").GetString();
        var list = await GetPublicAsync($"/api/v1/products?category={slug}&pageSize=50", pincode);
        return list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == productId);
    }

    private static async Task ExpectRefusalAsync(HttpClient client, string url, string code, string field)
    {
        var response = await client.GetAsync(url, Ct);
        var json = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, json.ToString());
        json.GetProperty("code").GetString().Should().Be(code);
        json.GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue(json.ToString());
    }

    private async Task ReceiveAsync(Guid locationId, Guid variantId, DateOnly caughtOn, DateTimeOffset bestBefore)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/inventory/receipts")
        {
            Content = JsonContent.Create(new
            {
                fulfillmentLocationId = locationId,
                variantId,
                batchCode = Unique("B"),
                supplierName = "Kasimedu landing",
                sourceLandingCenter = (string?)null,
                caughtOrHarvestedOn = caughtOn,
                receivedQuantity = new { value = 5000, unit = "GRAM" },
                totalCostPaise = 200000,
                bestBeforeAt = bestBefore,
                receiptTemperatureC = (decimal?)null,
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var operations = NewOperations();
        var response = await operations.SendAsync(request, Ct);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task CreateCouponAsync(string code, Guid cityId, string type, long value, long? maxDiscount, long minOrder, bool listed, Guid? productId)
    {
        using var operations = NewOperations();
        var response = await operations.PostAsJsonAsync("/api/v1/admin/coupons", new
        {
            code,
            description = (string?)null,
            discountType = type,
            discountValue = value,
            maxDiscountPaise = maxDiscount,
            minOrderPaise = minOrder,
            cityId,
            firstOrderOnly = false,
            perCustomerLimit = (int?)null,
            totalLimit = (int?)null,
            validFrom = Factory.Clock.UtcNow.AddHours(-1),
            validTo = (DateTimeOffset?)null,
            isListed = listed,
            scopes = productId is { } id ? new[] { new { scopeType = "PRODUCT", scopeId = id } } : [],
        }, Ct);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task SavePolicyAsync(Guid cityId, int maxOverweightBp)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/settings/order-policies/{cityId}")
        {
            Content = JsonContent.Create(new
            {
                reservationMinutes = 15,
                maxOverweightBp,
                cancelAllowedUntilStatus = "CONFIRMED",
                codMaxOrderPaise = 500000,
                enabledPaymentMethods = CodOnly,
                substitutionResponseMinutes = 15,
                complaintWindowHours = 24,
                maxDeliveryAttempts = 2,
            }),
        };
        request.Headers.IfNoneMatch.ParseAdd("*");
        using var operations = NewOperations();
        var response = await operations.SendAsync(request, Ct);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>An order placed through the Ordering domain (COD → CONFIRMED, ONLINE → PENDING_PAYMENT), one line per product.</summary>
    private async Task PlaceOrderAsync(Guid locationId, PaymentMethod method, params PublishedProduct[] products)
    {
        var at = Factory.Clock.UtcNow.AddHours(-2);
        var facts = new LinePriceFacts(PricedUnit.Gram, 49900, 0, null, 0);
        var amounts = LinePricing.Compute(facts, 500);
        var max = LinePricing.Compute(facts, 550);
        var items = products.Select(p => new OrderItemSnapshot(
            IdGenerator.NewId(), p.ProductId, Guid.CreateVersion7(), p.VariantId, "Product", "Medium", p.Sku, null, "0302",
            null, null, null, "KG", "GRAM", 49900, 0, null, 0, 10000, true, 500, 500, 550,
            amounts.ItemPaise, amounts.PreparationPaise, amounts.TotalPaise, max.TotalPaise, 0, 0)).ToList();
        var total = Money.Sum(items.Select(i => i.ItemPaise));
        var maxTotal = Money.Sum(items.Select(i => i.MaxLineTotalPaise));
        var order = Order.Place(new OrderPlacement(
            IdGenerator.NewId(),
            $"FO-T{Random.Shared.Next(1_000_000, 9_999_999)}",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            locationId,
            Guid.CreateVersion7(),
            "Asia/Kolkata",
            method,
            SubstitutionPreference.CallMe,
            new AddressSnapshot(Guid.CreateVersion7(), "Priya Raman", "+919876543210", "12, 3rd Cross Street", null, null, "Thiruvanmiyur", "Chennai", "TN", "600041", null, null),
            Guid.CreateVersion7(),
            at.AddHours(3),
            at.AddHours(5),
            null,
            Guid.NewGuid().ToString(),
            null,
            new OrderAmounts(total, 0, 0, 0, 0, 0, total, maxTotal),
            items,
            at,
            method == PaymentMethod.Online ? at.AddMinutes(15) : null));
        await using var db = Database.CreateDbContext();
        db.Add(order);
        await db.SaveChangesAsync(Ct);
    }
}
