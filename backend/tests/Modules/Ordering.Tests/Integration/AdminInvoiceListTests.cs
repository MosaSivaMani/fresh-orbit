using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Ordering.Domain.Invoices;

namespace FreshOrbit.Ordering.Tests.Integration;

/// <summary>
/// The staff invoice register <c>GET /api/v1/admin/invoices</c> (D353, D354): paging, invoice dates in the business
/// time zone, search, location scope and refusals, against real Postgres. Every test seeds its invoices at a location
/// of its own, so rows of other tests in the shared database never count.
/// </summary>
public sealed class AdminInvoiceListTests(OrderingDatabase database) : OrderingTestBase(database)
{
    private static readonly InvoiceSeller Seller = new("Fresh Orbit Foods Pvt Ltd", "33ABCDE1234F1Z5", "Chennai", CheckoutWorld.LocationFssai);

    [Fact]
    public async Task Lists_the_location_invoices_newest_first_with_amounts_buyer_and_pdf_state()
    {
        var location = Guid.CreateVersion7();
        var older = await SeedInvoiceAsync(location, new DateTimeOffset(2026, 10, 22, 6, 0, 0, TimeSpan.Zero), withPdf: false);
        var newer = await SeedInvoiceAsync(location, new DateTimeOffset(2026, 10, 23, 20, 0, 0, TimeSpan.Zero), withPdf: true);
        using var staff = StaffAt(location);

        var response = await staff.GetAsync(new Uri($"/api/v1/admin/invoices?fulfillmentLocationId={location}", UriKind.Relative), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadJsonAsync(response);
        page.GetProperty("totalCount").GetInt32().Should().Be(2);
        page.GetProperty("page").GetInt32().Should().Be(1);
        page.GetProperty("pageSize").GetInt32().Should().BeGreaterThan(0);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("id").GetGuid()).Should().Equal(newer.Id, older.Id);

        var first = items[0];
        first.GetProperty("invoiceNumber").GetString().Should().Be(newer.InvoiceNumber);
        first.GetProperty("issuedAt").GetDateTimeOffset().Should().Be(newer.IssuedAt);
        first.GetProperty("invoiceDate").GetString().Should().Be("2026-10-24", "20:00 UTC is 01:30 the next day in Asia/Kolkata");
        first.GetProperty("orderId").GetGuid().Should().Be(newer.OrderId);
        first.GetProperty("orderNumber").GetString().Should().Be(newer.OrderNumber);
        first.GetProperty("customerName").GetString().Should().Be("Priya Raman");
        first.GetProperty("fulfillmentLocationId").GetGuid().Should().Be(location);
        first.GetProperty("taxablePaise").GetInt64().Should().Be(newer.TaxablePaise);
        first.GetProperty("taxPaise").GetInt64().Should().Be(newer.TaxPaise);
        first.GetProperty("totalPaise").GetInt64().Should().Be(newer.TotalPaise);
        first.GetProperty("pdfReady").GetBoolean().Should().BeTrue();
        first.GetProperty("pdfUrl").GetString().Should().Be($"/api/v1/admin/orders/{newer.OrderId:D}/invoice/pdf");

        items[1].GetProperty("pdfReady").GetBoolean().Should().BeFalse();
        items[1].GetProperty("pdfUrl").ValueKind.Should().Be(JsonValueKind.Null);
        items[1].GetProperty("invoiceDate").GetString().Should().Be("2026-10-22");
    }

    [Fact]
    public async Task Pages_through_the_register()
    {
        var location = Guid.CreateVersion7();
        var a = await SeedInvoiceAsync(location, Start.AddHours(1));
        var b = await SeedInvoiceAsync(location, Start.AddHours(2));
        var c = await SeedInvoiceAsync(location, Start.AddHours(3));
        using var staff = StaffAt(location);

        var second = await ReadJsonAsync(await staff.GetAsync(new Uri("/api/v1/admin/invoices?page=2&pageSize=2", UriKind.Relative), Ct));

        second.GetProperty("totalCount").GetInt32().Should().Be(3);
        second.GetProperty("page").GetInt32().Should().Be(2);
        second.GetProperty("pageSize").GetInt32().Should().Be(2);
        second.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Equal(a.Id);
        (await IdsAsync(staff, "page=1&pageSize=2")).Should().Equal(c.Id, b.Id);
    }

    [Fact]
    public async Task Date_range_uses_invoice_dates_in_the_business_time_zone()
    {
        var location = Guid.CreateVersion7();
        var lateOn23 = await SeedInvoiceAsync(location, new DateTimeOffset(2026, 10, 23, 18, 0, 0, TimeSpan.Zero)); // 23:30 IST, 23 Oct
        var earlyOn24 = await SeedInvoiceAsync(location, new DateTimeOffset(2026, 10, 23, 19, 0, 0, TimeSpan.Zero)); // 00:30 IST, 24 Oct
        using var staff = StaffAt(location);

        (await IdsAsync(staff, "fromDate=2026-10-24&toDate=2026-10-24")).Should().Equal(earlyOn24.Id);
        (await IdsAsync(staff, "toDate=2026-10-23")).Should().Equal(lateOn23.Id);
        (await IdsAsync(staff, "fromDate=2026-10-23")).Should().Equal(earlyOn24.Id, lateOn23.Id);
        (await IdsAsync(staff, "fromDate=2026-10-25")).Should().BeEmpty();
    }

    [Fact]
    public async Task Search_matches_part_of_the_invoice_number_or_the_order_number()
    {
        var location = Guid.CreateVersion7();
        var one = await SeedInvoiceAsync(location, Start.AddHours(1));
        var two = await SeedInvoiceAsync(location, Start.AddHours(2));
        using var staff = StaffAt(location);

        (await IdsAsync(staff, $"q={Uri.EscapeDataString(one.InvoiceNumber[5..].ToUpperInvariant())}")).Should().Equal(one.Id);
        (await IdsAsync(staff, $"q={Uri.EscapeDataString(two.OrderNumber)}")).Should().Equal(two.Id);
        (await IdsAsync(staff, "q=%25")).Should().BeEmpty("a % in the search is a character, not a wildcard");
    }

    [Fact]
    public async Task Staff_see_only_their_locations_and_are_refused_another_location()
    {
        var mine = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        var own = await SeedInvoiceAsync(mine, Start.AddHours(1));
        await SeedInvoiceAsync(other, Start.AddHours(2));
        using var staff = StaffAt(mine);

        (await IdsAsync(staff, string.Empty)).Should().Equal(own.Id);

        var refused = await staff.GetAsync(new Uri($"/api/v1/admin/invoices?fulfillmentLocationId={other}", UriKind.Relative), Ct);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        CodeOf(await ReadJsonAsync(refused)).Should().Be("LOCATION_ACCESS_DENIED");
    }

    [Fact]
    public async Task Needs_orders_view()
    {
        using var packer = ClientFor(f => f.CreateStaffClient(["orders.pack"], [CheckoutWorld.LocationId]));

        var response = await packer.GetAsync(new Uri("/api/v1/admin/invoices", UriKind.Relative), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("fromDate=24-10-2026", "fromDate")]
    [InlineData("toDate=2026-13-01", "toDate")]
    [InlineData("fromDate=2026-10-24&toDate=2026-10-23", "toDate")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("page=0", "page")]
    public async Task Refuses_bad_filters_on_the_field(string filter, string field)
    {
        using var staff = StaffAt(CheckoutWorld.LocationId);

        var response = await staff.GetAsync(new Uri($"/api/v1/admin/invoices?{filter}", UriKind.Relative), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await ReadJsonAsync(response);
        CodeOf(problem).Should().Be("VALIDATION_FAILED");
        problem.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Refuses_without_a_business_time_zone()
    {
        World.Profile = World.Profile with { TimeZone = string.Empty };
        using var staff = StaffAt(CheckoutWorld.LocationId);

        var response = await staff.GetAsync(new Uri("/api/v1/admin/invoices", UriKind.Relative), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await ReadJsonAsync(response);
        CodeOf(problem).Should().Be("CONFIGURATION_MISSING");
        problem.GetProperty("errors").TryGetProperty("timeZone", out _).Should().BeTrue();
    }

    private HttpClient StaffAt(Guid location) => ClientFor(f => f.CreateStaffClient(["orders.view"], [location]));

    private static async Task<List<Guid>> IdsAsync(HttpClient staff, string query)
    {
        var response = await staff.GetAsync(new Uri($"/api/v1/admin/invoices?{query}", UriKind.Relative), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return [.. (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
    }

    /// <summary>A packed order at <paramref name="location"/> with its invoice issued at <paramref name="issuedAt"/>.</summary>
    private async Task<SeededInvoice> SeedInvoiceAsync(Guid location, DateTimeOffset issuedAt, bool withPdf = false)
    {
        await using var db = Database.CreateDbContext();
        var order = OrderFactory.Place([OrderFactory.SeerFish(), OrderFactory.Crabs()], fulfillmentLocationId: location, placedAt: Start, orderNumber: $"FO-T{Random.Shared.Next(10_000_000, 99_999_999)}").Preparing().PackedAsOrdered();
        var invoice = Invoice.Issue(order, $"INV/t{Guid.NewGuid():N}"[..16], Seller, "996813", issuedAt);
        if (withPdf)
        {
            invoice.SetPdf(Invoice.PdfKey(invoice.Id));
        }

        db.Add(order);
        db.Add(invoice);
        await db.SaveChangesAsync(Ct);
        return new SeededInvoice(invoice.Id, invoice.InvoiceNumber, issuedAt, order.Id, order.OrderNumber, invoice.TaxablePaise, invoice.TaxPaise, invoice.TotalPaise);
    }

    private sealed record SeededInvoice(Guid Id, string InvoiceNumber, DateTimeOffset IssuedAt, Guid OrderId, string OrderNumber, long TaxablePaise, long TaxPaise, long TotalPaise);
}
