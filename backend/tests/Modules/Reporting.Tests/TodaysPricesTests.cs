using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using static FreshOrbit.Reporting.Tests.BuyingDataset;
using static FreshOrbit.Reporting.Tests.BuyingListTests;

namespace FreshOrbit.Reporting.Tests;

/// <summary>REP-R15 "today's prices" against real Postgres (buying fixture, clock 2026-11-05 17:30 IST).</summary>
public sealed class TodaysPricesTests(ReportingDatabase database) : ReportingTestBase(database)
{
    private static readonly string Url = $"/api/v1/admin/reports/todays-prices?fulfillmentLocationId={L3}";

    private HttpClient Pricing() => Factory.CreateStaffClient(["pricing.manage"], new[] { L3 });

    [Fact]
    public async Task Lists_price_latest_batch_cost_margin_and_a_suggestion_per_variant()
    {
        using var client = Pricing();
        Factory.Clock.Set(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));

        var prices = await GetJsonAsync(client, $"{Url}&targetMarginBp=3000&categoryTargets={SeaFish}:2500");

        prices.GetProperty("locationCode").GetString().Should().Be("L3");
        prices.GetProperty("tomorrowStartsAt").GetDateTimeOffset().Should().Be(new DateTimeOffset(2026, 11, 5, 18, 30, 0, TimeSpan.Zero));
        prices.GetProperty("targetMarginBp").GetInt32().Should().Be(3000);
        var skus = prices.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString()).ToList();
        skus.Should().Contain(new[] { "POMF-KG", "SEER-STEAK-500", "LOBSTER-PC" }).And.NotContain("SARDINE-KG", "archived products are not priced");
        prices.GetProperty("totalCount").GetInt32().Should().Be(skus.Count);

        var pomfret = Item(prices, PomfretVariant);
        AssertNumbers(pomfret, new()
        {
            ["unitPricePaise"] = 90000,
            ["nextUnitPricePaise"] = 95000,
            ["taxRateBp"] = 500,
            ["yieldBp"] = 6500,
            ["latestBatchCostPaise"] = 455000,
            ["latestBatchQuantity"] = 10000,
            ["costPerUnitPaise"] = 70000, // ₹455 per raw kg ÷ 0.65 yield
            ["marginBp"] = 1833, // (85 714 ex GST − 70 000) / 85 714
            ["targetMarginBp"] = 2500, // the category target wins over the screen target
            ["suggestedPricePaise"] = 98100, // 70 000 ÷ 0.75 = 93 334, + 5 % GST = 98 001, up to whole rupees
        });
        pomfret.GetProperty("preparationCode").GetString().Should().Be("CURRY_CUT");
        pomfret.GetProperty("categoryName").GetString().Should().Be("Sea fish");
        pomfret.GetProperty("issues").GetArrayLength().Should().Be(0);

        var packs = Item(prices, SeerPackVariant);
        AssertNumbers(packs, new()
        {
            ["unitPricePaise"] = 65000,
            ["compareAtPaise"] = 70000,
            ["costPerUnitPaise"] = 50000, // ₹10 000 ÷ 20 packs; the pack weight is already net
            ["marginBp"] = 1923,
            ["targetMarginBp"] = 3000,
            ["suggestedPricePaise"] = 75100,
        });
        packs.GetProperty("sellUnit").GetString().Should().Be("PACK");

        var lobster = Item(prices, LobsterVariant);
        lobster.GetProperty("issues").EnumerateArray().Select(i => i.GetString())
            .Should().Equal("PRICE_MISSING", "BATCH_MISSING", "TAX_RULE_MISSING");
        lobster.GetProperty("unitPricePaise").ValueKind.Should().Be(JsonValueKind.Null, "the L1 price is not a price at L3");
        lobster.GetProperty("costPerUnitPaise").ValueKind.Should().Be(JsonValueKind.Null);
        lobster.GetProperty("suggestedPricePaise").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Without_a_target_there_is_no_suggestion_and_the_category_filter_narrows()
    {
        using var client = Pricing();
        Factory.Clock.Set(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));

        var prices = await GetJsonAsync(client, $"{Url}&categoryId={SeaFish}");

        prices.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString()).Should().Equal("POMF-KG");
        var pomfret = Item(prices, PomfretVariant);
        pomfret.GetProperty("marginBp").GetInt32().Should().Be(1833);
        pomfret.GetProperty("targetMarginBp").ValueKind.Should().Be(JsonValueKind.Null);
        pomfret.GetProperty("suggestedPricePaise").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Refuses_bad_targets_other_locations_and_staff_without_pricing()
    {
        using var client = Pricing();

        var target = await client.GetAsync($"{Url}&targetMarginBp=9600", Ct);
        target.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await FieldsAsync(target)).Should().Contain("targetMarginBp");

        var pairs = await client.GetAsync($"{Url}&categoryTargets=sea-fish:2500", Ct);
        (await FieldsAsync(pairs)).Should().Contain("categoryTargets");

        var high = await client.GetAsync($"{Url}&categoryTargets={SeaFish}:9900", Ct);
        (await FieldsAsync(high)).Should().Contain("categoryTargets");

        var denied = await Factory.CreateStaffClient(["pricing.manage"], new[] { ReportingDataset.L1 }).GetAsync(Url, Ct);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeAsync(denied)).Should().Be("LOCATION_ACCESS_DENIED");

        var forbidden = await Factory.CreateStaffClient(["inventory.view", "reports.view"], new[] { L3 }).GetAsync(Url, Ct);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
