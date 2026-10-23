using AwesomeAssertions;
using FreshOrbit.Reporting.Domain;
using Microsoft.EntityFrameworkCore;
using static FreshOrbit.Reporting.Tests.ReportingDataset;

namespace FreshOrbit.Reporting.Tests;

public sealed class RefreshJobTests(ReportingDatabase database) : ReportingTestBase(database)
{
    [Fact]
    public async Task T1_T2_refresh_runs_only_when_sources_changed_and_then_includes_the_new_order()
    {
        await RefreshAsync();
        var before = await StatesAsync();

        var idle = await RefreshAsync();

        idle.Skipped.Should().BeTrue();
        (await StatesAsync()).Should().BeEquivalentTo(before, "T1: nothing newer than the stored watermark");

        var orderId = Guid.CreateVersion7();
        await ExecuteAsync($"""
            INSERT INTO ordering.orders (id, order_number, customer_id, city_id, fulfillment_location_id, status, total_paise, final_total_paise, placed_at, delivered_at)
            VALUES ('{orderId}', 'FO-269999', '{Guid.CreateVersion7()}', '{City}', '{L2}', 'DELIVERED', 12345, 12345, '2026-11-02T04:00:00Z', '2026-11-02T06:00:00Z');
            """);
        using var client = Reports(all: true);
        const string url = "/api/v1/admin/reports/daily-sales?fromDate=2026-11-02&toDate=2026-11-02";

        var stale = await GetJsonAsync(client, url);
        stale.GetProperty("totalCount").GetInt32().Should().Be(0, "T2: the view keeps its data until the next run");

        var run = await RefreshAsync();
        run.Refreshed.Should().HaveCount(5);
        var fresh = await GetJsonAsync(client, url);
        fresh.GetProperty("totalCount").GetInt32().Should().Be(1);
        fresh.GetProperty("totals").GetProperty("netRevenuePaise").GetInt64().Should().Be(12345);
        (await StatesAsync()).Values.Should().OnlyContain(s => s.LastError == null && s.RefreshedAt != null);
    }

    private async Task<Dictionary<string, RefreshState>> StatesAsync()
    {
        await using var db = Database.CreateDbContext();
        return await db.Set<RefreshState>().AsNoTracking().ToDictionaryAsync(s => s.ViewName, Ct);
    }
}
