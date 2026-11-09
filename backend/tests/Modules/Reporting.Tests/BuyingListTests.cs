using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using static FreshOrbit.Reporting.Tests.BuyingDataset;
using static FreshOrbit.Reporting.Tests.ReportingDataset;

namespace FreshOrbit.Reporting.Tests;

/// <summary>REP-R14 buying list against real Postgres (docs/product/reporting.md §6, buying fixture).</summary>
public sealed class BuyingListTests(ReportingDatabase database) : ReportingTestBase(database)
{
    private static readonly string Url = $"/api/v1/admin/reports/buying-list?fulfillmentLocationId={L3}&deliveryDate=2026-11-06&safetyMarginBp=1000";

    private HttpClient Buyer(IReadOnlyCollection<Guid>? locations = null) => Factory.CreateStaffClient(["inventory.view"], locations ?? new[] { L3 });

    [Fact]
    public async Task Sums_confirmed_lines_per_variant_and_preparation_with_margin_stock_and_shortfall()
    {
        var list = await GetJsonAsync(Buyer(), Url);

        list.GetProperty("deliveryDate").GetString().Should().Be("2026-11-06");
        list.GetProperty("locationCode").GetString().Should().Be("L3");
        list.GetProperty("safetyMarginBp").GetInt32().Should().Be(1000);
        list.GetProperty("ordersCount").GetInt32().Should().Be(2, "O1 CONFIRMED and O2 PREPARING; cancelled, pending and next-day orders are out");
        list.GetProperty("pendingPaymentOrders").GetInt32().Should().Be(1);
        list.GetProperty("totalCount").GetInt32().Should().Be(3);
        list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString())
            .Should().Equal("LOBSTER-PC", "SEER-STEAK-500", "POMF-KG");

        var pomfret = Item(list, PomfretVariant);
        AssertNumbers(pomfret, new()
        {
            ["ordersCount"] = 2,
            ["orderedQuantity"] = 2500,
            ["orderedNetGrams"] = 2500,
            ["neededQuantity"] = 3309, // 770 + 1 539 curry cut at 6 500 bp, 1 000 whole
            ["estimatedRawGrams"] = 3309,
            ["withMarginQuantity"] = 3640, // ceil(3 309 × 1.1)
            ["withMarginRawGrams"] = 3640,
            ["onHand"] = 5300,
            ["reservedForOtherOrders"] = 1924, // 5 233 reserved − 3 309 held by the listed lines (pending O4 and next-day O5)
            ["availableQuantity"] = 3376,
            ["shortfallQuantity"] = 264,
            ["shortfallRawGrams"] = 264,
        });
        pomfret.GetProperty("stockUnit").GetString().Should().Be("GRAM");
        pomfret.GetProperty("stockItemMissing").GetBoolean().Should().BeFalse();
        var preparations = pomfret.GetProperty("preparations").EnumerateArray().ToList();
        preparations.Select(p => p.GetProperty("preparationCode").GetString()).Should().Equal("CURRY_CUT", "WHOLE");
        AssertNumbers(preparations[0], new() { ["yieldBp"] = 6500, ["ordersCount"] = 2, ["orderedQuantity"] = 1500, ["neededQuantity"] = 2309, ["estimatedRawGrams"] = 2309 });
        AssertNumbers(preparations[1], new() { ["yieldBp"] = 10000, ["ordersCount"] = 1, ["orderedQuantity"] = 1000, ["neededQuantity"] = 1000, ["estimatedRawGrams"] = 1000 });

        var packs = Item(list, SeerPackVariant);
        AssertNumbers(packs, new()
        {
            ["orderedQuantity"] = 2,
            ["orderedNetGrams"] = 1000, // 2 packs × 500 g net
            ["neededQuantity"] = 2,
            ["estimatedRawGrams"] = 1429, // ceil(1 000 × 10 000 / 7 000)
            ["withMarginQuantity"] = 3,
            ["withMarginRawGrams"] = 1572,
            ["availableQuantity"] = 0,
            ["shortfallQuantity"] = 3,
            ["shortfallRawGrams"] = 1572,
        });
        packs.GetProperty("stockItemMissing").GetBoolean().Should().BeTrue();
        packs.GetProperty("onHand").ValueKind.Should().Be(JsonValueKind.Null);

        var lobster = Item(list, LobsterVariant);
        AssertNumbers(lobster, new() { ["orderedQuantity"] = 3, ["neededQuantity"] = 3, ["withMarginQuantity"] = 4, ["availableQuantity"] = 10, ["shortfallQuantity"] = 0 });
        lobster.GetProperty("orderedNetGrams").ValueKind.Should().Be(JsonValueKind.Null, "piece weights are never guessed");
        lobster.GetProperty("estimatedRawGrams").ValueKind.Should().Be(JsonValueKind.Null);
        lobster.GetProperty("shortfallRawGrams").ValueKind.Should().Be(JsonValueKind.Null);

        list.GetProperty("estimatedRawGrams").GetInt64().Should().Be(3640 + 1572);
        list.GetProperty("shortfallRawGrams").GetInt64().Should().Be(264 + 1572);
    }

    [Fact]
    public async Task Without_a_margin_or_date_the_list_is_for_today_at_the_location()
    {
        using var client = Buyer();
        Factory.Clock.Set(new DateTimeOffset(2026, 11, 5, 20, 0, 0, TimeSpan.Zero)); // 2026-11-06 01:30 IST

        var list = await GetJsonAsync(client, $"/api/v1/admin/reports/buying-list?fulfillmentLocationId={L3}");

        list.GetProperty("deliveryDate").GetString().Should().Be("2026-11-06");
        list.GetProperty("safetyMarginBp").GetInt32().Should().Be(0);
        AssertNumbers(Item(list, PomfretVariant), new() { ["withMarginQuantity"] = 3309, ["shortfallQuantity"] = 0 });
    }

    [Fact]
    public async Task Csv_lists_variant_and_preparation_rows_and_a_total()
    {
        var response = await Buyer().GetAsync(Url + "&format=csv", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain("buying_list_L3_2026-11-06.csv");
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var lines = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().StartWith("row_type,product_name,variant_name,sku,preparation_code");
        lines.Count(l => l.StartsWith("VARIANT,", StringComparison.Ordinal)).Should().Be(3);
        lines.Count(l => l.StartsWith("PREPARATION,", StringComparison.Ordinal)).Should().Be(4);
        lines.Should().Contain(l => l.StartsWith("VARIANT,Silver Pomfret,Pomfret per kg,POMF-KG,", StringComparison.Ordinal) && l.EndsWith(",264,264", StringComparison.Ordinal));
        lines[^1].Should().StartWith("TOTAL,").And.EndWith(",1836");
    }

    [Fact]
    public async Task Refuses_a_missing_location_a_bad_margin_and_other_locations()
    {
        using var buyer = Buyer();

        var missing = await buyer.GetAsync("/api/v1/admin/reports/buying-list", Ct);
        missing.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await FieldsAsync(missing)).Should().Contain("fulfillmentLocationId");

        var margin = await buyer.GetAsync($"/api/v1/admin/reports/buying-list?fulfillmentLocationId={L3}&safetyMarginBp=10001", Ct);
        margin.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await FieldsAsync(margin)).Should().Contain("safetyMarginBp");

        var date = await buyer.GetAsync($"/api/v1/admin/reports/buying-list?fulfillmentLocationId={L3}&deliveryDate=06-11-2026", Ct);
        (await FieldsAsync(date)).Should().Contain("deliveryDate");

        var denied = await Buyer([L1]).GetAsync(Url, Ct);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeAsync(denied)).Should().Be("LOCATION_ACCESS_DENIED");

        var forbidden = await Factory.CreateStaffClient(["reports.view"], [L3]).GetAsync(Url, Ct);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeAsync(forbidden)).Should().Be("FORBIDDEN");
    }

    internal static JsonElement Item(JsonElement list, Guid variantId)
        => list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("variantId").GetGuid() == variantId);

    internal static void AssertNumbers(JsonElement row, Dictionary<string, long> expected)
    {
        foreach (var (name, value) in expected)
        {
            row.GetProperty(name).GetInt64().Should().Be(value, name);
        }
    }

    internal static async Task<List<string>> FieldsAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var problem = document.RootElement;
        problem.GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
        return [.. problem.GetProperty("errors").EnumerateObject().Select(e => e.Name)];
    }
}
