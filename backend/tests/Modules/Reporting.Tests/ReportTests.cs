using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using static FreshOrbit.Reporting.Tests.ReportingDataset;

namespace FreshOrbit.Reporting.Tests;

public sealed class ReportTests(ReportingDatabase database) : ReportingTestBase(database)
{
    private const string Day = "fromDate=2026-10-23&toDate=2026-10-23";

    [Fact]
    public async Task R1_daily_sales_of_the_seeded_day()
    {
        await RefreshAsync();

        var report = await GetJsonAsync(Reports([L1]), $"/api/v1/admin/reports/daily-sales?{Day}");

        report.GetProperty("totalCount").GetInt32().Should().Be(1);
        var row = report.GetProperty("items")[0];
        row.GetProperty("businessDate").GetString().Should().Be("2026-10-23");
        row.GetProperty("cityId").GetGuid().Should().Be(City);
        AssertNumbers(row, new()
        {
            ["ordersPlaced"] = 4, ["ordersDelivered"] = 2, ["ordersCancelled"] = 2, ["grossPaise"] = 315306, ["discountPaise"] = 10000,
            ["deliveryFeePaise"] = 0, ["taxPaise"] = 0, ["netRevenuePaise"] = 305306, ["refundsPaise"] = 59645,
            ["postDeliveryRefundsPaise"] = 5000, ["avgOrderValuePaise"] = 152653,
        });
        AssertNumbers(report.GetProperty("totals"), new() { ["netRevenuePaise"] = 305306, ["avgOrderValuePaise"] = 152653, ["ordersDelivered"] = 2 });
        report.GetProperty("asOf").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task R2_product_performance_matches_daily_gross()
    {
        await RefreshAsync();

        var report = await GetJsonAsync(Reports([L1]), $"/api/v1/admin/reports/product-performance?{Day}&sort=sku");

        var items = report.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(3);
        var seer = items.Single(i => i.GetProperty("sku").GetString() == "SEER-KG");
        seer.GetProperty("preparationCode").GetString().Should().Be("STEAK");
        AssertNumbers(seer, new()
        {
            ["ordersCount"] = 2, ["orderedQuantity"] = 1000, ["packedQuantity"] = 990, ["chargedQuantity"] = 990,
            ["revenuePaise"] = 132561, ["saleCostPaise"] = 123105, ["grossMarginPaise"] = 9456,
        });
        var prawn = items.Single(i => i.GetProperty("sku").GetString() == "PRAWN-KG");
        AssertNumbers(prawn, new() { ["packedQuantity"] = 580, ["chargedQuantity"] = 550, ["revenuePaise"] = 52745, ["saleCostPaise"] = 58020, ["grossMarginPaise"] = -5275 });
        var crab = items.Single(i => i.GetProperty("sku").GetString() == "CRAB-PC");
        crab.GetProperty("stockUnit").GetString().Should().Be("PIECE");
        AssertNumbers(crab, new() { ["packedQuantity"] = 2, ["revenuePaise"] = 130000, ["saleCostPaise"] = 80000, ["grossMarginPaise"] = 50000 });
        report.GetProperty("totals").GetProperty("revenuePaise").GetInt64().Should().Be(315306);

        var filtered = await GetJsonAsync(Reports([L1]), $"/api/v1/admin/reports/product-performance?{Day}&categoryId={Category}");
        filtered.GetProperty("items").EnumerateArray().Should().ContainSingle().Which.GetProperty("sku").GetString().Should().Be("SEER-KG");
    }

    [Fact]
    public async Task R3_contribution_margin()
    {
        await RefreshAsync();

        var report = await GetJsonAsync(Reports([L1]), $"/api/v1/admin/reports/contribution-margin?{Day}");

        AssertNumbers(report.GetProperty("items")[0], new()
        {
            ["revenueExTaxPaise"] = 305306, ["saleCostPaise"] = 261125, ["refundsPaise"] = 5000, ["deliveryCostPaise"] = 0,
            ["contributionPaise"] = 39181, ["contributionMarginBp"] = 1283,
        });
        report.GetProperty("totals").GetProperty("contributionMarginBp").GetInt64().Should().Be(1283);
    }

    [Fact]
    public async Task R4_waste_summary()
    {
        await RefreshAsync();

        var report = await GetJsonAsync(Reports([L1]), $"/api/v1/admin/reports/waste?{Day}&reasonCode=SPOILED");

        var row = report.GetProperty("items").EnumerateArray().Should().ContainSingle().Which;
        row.GetProperty("variantId").GetGuid().Should().Be(SeerVariant);
        row.GetProperty("productName").GetString().Should().Be("Seer Fish");
        AssertNumbers(row, new() { ["movementsCount"] = 1, ["wasteQuantity"] = 1000, ["wasteCostPaise"] = 87000, ["soldQuantity"] = 1415, ["wasteShareBp"] = 4140 });
    }

    [Fact]
    public async Task R5_repeat_customers_for_october()
    {
        await RefreshAsync();

        var report = await GetJsonAsync(Reports([L1]), "/api/v1/admin/reports/repeat-customers?fromDate=2026-10-01&toDate=2026-10-31");

        AssertNumbers(report.GetProperty("totals"), new() { ["activeCustomers"] = 2, ["repeatCustomers"] = 1, ["repeatRateBp"] = 5000, ["newCustomers"] = 1 });
        var cohort = report.GetProperty("items").EnumerateArray().Should().ContainSingle().Which;
        cohort.GetProperty("cohortMonth").GetString().Should().Be("2026-10-01");
        cohort.GetProperty("cohortSize").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task T3_late_evening_delivery_counts_on_the_local_date()
    {
        await RefreshAsync();

        var next = await GetJsonAsync(Reports([L1]), "/api/v1/admin/reports/daily-sales?fromDate=2026-10-24&toDate=2026-10-24");

        next.GetProperty("totalCount").GetInt32().Should().Be(0, "order B delivered 18:00 UTC is 23:30 IST on 2026-10-23");
    }

    [Fact]
    public async Task T4_scoped_staff_see_only_their_locations()
    {
        await RefreshAsync();

        var scoped = await GetJsonAsync(Reports([L1]), $"/api/v1/admin/reports/daily-sales?{Day}");
        scoped.GetProperty("totals").GetProperty("netRevenuePaise").GetInt64().Should().Be(305306);

        var all = await GetJsonAsync(Reports(all: true), $"/api/v1/admin/reports/daily-sales?{Day}");
        all.GetProperty("totals").GetProperty("netRevenuePaise").GetInt64().Should().Be(305306 + 99999);
        all.GetProperty("totalCount").GetInt32().Should().Be(2);

        var byCity = await GetJsonAsync(Reports([L1]), $"/api/v1/admin/reports/daily-sales?{Day}&cityId={City}");
        byCity.GetProperty("totalCount").GetInt32().Should().Be(1);

        var denied = await Reports([L1]).GetAsync($"/api/v1/admin/reports/daily-sales?{Day}&fulfillmentLocationId={L2}", Ct);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeAsync(denied)).Should().Be("LOCATION_ACCESS_DENIED");

        var inconsistent = await Reports(all: true).GetAsync($"/api/v1/admin/reports/daily-sales?{Day}&fulfillmentLocationId={L2}&cityId={Guid.CreateVersion7()}", Ct);
        inconsistent.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task T5_csv_export_has_bom_header_neutralised_text_and_total_row()
    {
        await RefreshAsync();
        using var client = Reports(all: true);

        var response = await client.GetAsync($"/api/v1/admin/reports/product-performance?{Day}&format=csv", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain("product_performance_2026-10-23_2026-10-23.csv");
        response.Headers.Contains("X-Report-As-Of").Should().BeTrue();
        response.Headers.CacheControl!.ToString().Should().Contain("max-age=60");
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var lines = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().StartWith("business_date,fulfillment_location_id,product_id,variant_id,sku,product_name");
        lines[0].Should().Contain("revenue_paise,revenue_inr");
        lines.Should().Contain(l => l.Contains("\"'=HYPERLINK(\"\"http://x\"\")\"", StringComparison.Ordinal));
        lines[^1].Should().StartWith("TOTAL,");
        lines[^1].Should().Contain("415305,4153.05", "Σ revenue at L1 and L2 = 315306 + 99999");

        var json = await GetJsonAsync(client, $"/api/v1/admin/reports/product-performance?{Day}");
        json.GetProperty("totals").GetProperty("revenuePaise").GetInt64().Should().Be(415305);
    }

    [Fact]
    public async Task T6_invalid_ranges_are_refused()
    {
        using var client = Reports(all: true);

        (await client.GetAsync("/api/v1/admin/reports/daily-sales?fromDate=2025-01-01&toDate=2026-02-04", Ct)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var inverted = await client.GetAsync("/api/v1/admin/reports/daily-sales?fromDate=2026-10-24&toDate=2026-10-23", Ct);
        inverted.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeAsync(inverted)).Should().Be("VALIDATION_FAILED");
        (await client.GetAsync("/api/v1/admin/reports/daily-sales?sort=nope", Ct)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.GetAsync("/api/v1/admin/reports/dashboard?format=csv", Ct)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task T8_reports_need_the_reports_view_permission()
    {
        var response = await Factory.CreateStaffClient(["payments.view"], allLocations: true).GetAsync("/api/v1/admin/reports/daily-sales", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeAsync(response)).Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Dashboard_tiles_are_scoped()
    {
        var scoped = await GetJsonAsync(Reports([L1]), "/api/v1/admin/reports/dashboard");
        scoped.GetProperty("openComplaints").GetInt64().Should().Be(1);
        scoped.GetProperty("refundsRequested").GetInt64().Should().Be(1);
        scoped.GetProperty("refundsRequestedPaise").GetInt64().Should().Be(1500);
        scoped.GetProperty("openPaymentIssues").GetInt64().Should().Be(1);
        scoped.GetProperty("lowStock").EnumerateArray().Should().ContainSingle().Which.GetProperty("variantId").GetGuid().Should().Be(PrawnVariant);

        var all = await GetJsonAsync(Reports(all: true), "/api/v1/admin/reports/dashboard");
        all.GetProperty("openPaymentIssues").GetInt64().Should().Be(2);
        all.GetProperty("lowStock").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Dashboard_sales_tiles_are_server_computed_for_the_callers_locations()
    {
        using var scopedClient = Reports([L1]);
        using var allClient = Reports(all: true);
        Factory.Clock.Set(new DateTimeOffset(2026, 10, 23, 10, 0, 0, TimeSpan.Zero)); // 15:30 IST; tokens were issued at real time

        var scoped = await GetJsonAsync(scopedClient, "/api/v1/admin/reports/dashboard");
        var all = await GetJsonAsync(allClient, "/api/v1/admin/reports/dashboard");

        // L1 placed A, B, C, D on 2026-10-23 IST; C and D were cancelled; A and B delivered the same day.
        AssertNumbers(scoped.GetProperty("sales"), new()
        {
            ["ordersPlaced"] = 4,
            ["ordersCancelled"] = 2,
            ["orderValuePaise"] = 245051 + 60255,
            ["averageOrderValuePaise"] = (245051 + 60255) / 2,
            ["ordersDelivered"] = 2,
            ["deliveredRevenuePaise"] = 245051 + 60255,
        });
        // F at L2 adds one placed and delivered order.
        AssertNumbers(all.GetProperty("sales"), new()
        {
            ["ordersPlaced"] = 5,
            ["ordersCancelled"] = 2,
            ["orderValuePaise"] = 245051 + 60255 + 99999,
            ["averageOrderValuePaise"] = (245051 + 60255 + 99999) / 3,
            ["ordersDelivered"] = 3,
            ["deliveredRevenuePaise"] = 245051 + 60255 + 99999,
        });

        var prawns = scoped.GetProperty("lowStock").EnumerateArray().Single();
        prawns.GetProperty("stockUnit").GetString().Should().Be("GRAM");
        prawns.GetProperty("productName").GetString().Should().Be("Tiger Prawns");
        prawns.GetProperty("available").GetInt64().Should().Be(900);
    }

    [Fact]
    public async Task Dashboard_sales_are_zero_on_a_day_without_orders()
    {
        using var client = Reports(all: true);
        Factory.Clock.Set(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));

        var sales = (await GetJsonAsync(client, "/api/v1/admin/reports/dashboard")).GetProperty("sales");

        sales.GetProperty("ordersPlaced").GetInt64().Should().Be(0);
        sales.GetProperty("orderValuePaise").GetInt64().Should().Be(0);
        sales.GetProperty("averageOrderValuePaise").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    private static void AssertNumbers(JsonElement row, Dictionary<string, long> expected)
    {
        foreach (var (name, value) in expected)
        {
            row.GetProperty(name).GetInt64().Should().Be(value, name);
        }
    }
}
