using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.E1Testing;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Customers.Tests.Integration;

/// <summary>G6: a customer's orders, order counts on the detail and the <c>cityId</c> filter, all location-scoped.</summary>
public sealed class CustomerOrdersTests(E1Database database) : E1ApiTestBase(database)
{
    private static readonly DateTimeOffset Day = new(2026, 10, 20, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Orders_and_counts_follow_the_callers_location_scope()
    {
        var customerId = await NewCustomerAsync();
        var (here, elsewhere) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await InsertOrderAsync(customerId, here, "DELIVERED", Day.AddHours(1), 50_000, 48_500);
        await InsertOrderAsync(customerId, here, "CANCELLED", Day.AddHours(2), 30_000, null);
        await InsertOrderAsync(customerId, here, "CONFIRMED", Day.AddHours(3), 20_000, null);
        await InsertOrderAsync(customerId, here, "PAYMENT_FAILED", Day.AddHours(4), 10_000, null);
        await InsertOrderAsync(customerId, elsewhere, "DELIVERED", Day.AddHours(5), 99_000, 99_000);
        using var all = Factory.CreateStaffClient(["customers.view", "orders.view"], allLocations: true);
        using var scoped = Factory.CreateStaffClient(["customers.view", "orders.view"], [here]);

        var allOrders = await GetOkAsync(all, $"/api/v1/admin/customers/{customerId}/orders?pageSize=100");
        allOrders.GetProperty("totalCount").GetInt32().Should().Be(5);
        var rows = allOrders.GetProperty("items").EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("status").GetString()).Should().Equal("DELIVERED", "PAYMENT_FAILED", "CONFIRMED", "CANCELLED", "DELIVERED");
        rows[0].GetProperty("fulfillmentLocationId").GetGuid().Should().Be(elsewhere);
        rows[4].GetProperty("totalPaise").GetInt64().Should().Be(50_000);
        rows[4].GetProperty("finalTotalPaise").GetInt64().Should().Be(48_500);
        rows[4].GetProperty("paymentMethod").GetString().Should().Be("COD");
        rows[4].GetProperty("orderNumber").GetString().Should().StartWith("T-");

        var scopedOrders = await GetOkAsync(scoped, $"/api/v1/admin/customers/{customerId}/orders?pageSize=2&page=2");
        scopedOrders.GetProperty("totalCount").GetInt32().Should().Be(4);
        scopedOrders.GetProperty("page").GetInt32().Should().Be(2);
        scopedOrders.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("status").GetString()).Should().Equal("CANCELLED", "DELIVERED");

        var allCounts = (await GetOkAsync(all, $"/api/v1/admin/customers/{customerId}")).GetProperty("orderCounts");
        AssertCounts(allCounts, total: 4, open: 1, delivered: 2, cancelled: 1);
        allCounts.GetProperty("lastPlacedAt").GetDateTimeOffset().Should().Be(Day.AddHours(5));
        AssertCounts((await GetOkAsync(scoped, $"/api/v1/admin/customers/{customerId}")).GetProperty("orderCounts"), total: 3, open: 1, delivered: 1, cancelled: 1);
    }

    [Fact]
    public async Task A_customer_without_orders_has_zero_counts_and_an_empty_page()
    {
        var customerId = await NewCustomerAsync();
        using var staff = Factory.CreateStaffClient(["customers.view", "orders.view"], allLocations: true);

        var detail = await GetOkAsync(staff, $"/api/v1/admin/customers/{customerId}");
        var orders = await GetOkAsync(staff, $"/api/v1/admin/customers/{customerId}/orders");

        AssertCounts(detail.GetProperty("orderCounts"), total: 0, open: 0, delivered: 0, cancelled: 0);
        detail.GetProperty("orderCounts").GetProperty("lastPlacedAt").ValueKind.Should().Be(JsonValueKind.Null);
        orders.GetProperty("items").GetArrayLength().Should().Be(0);
        orders.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Customer_orders_need_orders_view_and_an_existing_customer()
    {
        var customerId = await NewCustomerAsync();
        using var viewerOnly = Factory.CreateStaffClient(["customers.view"], allLocations: true);
        using var staff = Factory.CreateStaffClient(["customers.view", "orders.view"], allLocations: true);

        (await viewerOnly.GetAsync($"/api/v1/admin/customers/{customerId}/orders", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.GetAsync($"/api/v1/admin/customers/{Guid.CreateVersion7()}/orders", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await staff.GetAsync($"/api/v1/admin/customers/{customerId}/orders?pageSize=0", Ct)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task City_filter_keeps_customers_with_a_saved_address_in_a_pincode_of_the_city()
    {
        var (cityId, pincode) = await CreateCityWithPincodeAsync();
        var inCity = await NewCustomerAsync(pincode);
        var outside = await NewCustomerAsync("600040");
        using var staff = Factory.CreateStaffClient(["customers.view"]);

        var page = await GetOkAsync(staff, $"/api/v1/admin/customers?cityId={cityId}&pageSize=100");
        var other = await GetOkAsync(staff, $"/api/v1/admin/customers?cityId={Guid.CreateVersion7()}");

        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("customerId").GetGuid()).Should().Equal(inCity);
        page.GetProperty("totalCount").GetInt32().Should().Be(1);
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("customerId").GetGuid()).Should().NotContain(outside);
        other.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    private static void AssertCounts(JsonElement counts, int total, int open, int delivered, int cancelled)
    {
        counts.GetProperty("total").GetInt32().Should().Be(total);
        counts.GetProperty("open").GetInt32().Should().Be(open);
        counts.GetProperty("delivered").GetInt32().Should().Be(delivered);
        counts.GetProperty("cancelled").GetInt32().Should().Be(cancelled);
    }

    private async Task<Guid> NewCustomerAsync(string? addressPincode = null)
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        if (addressPincode is not null)
        {
            using var client = Factory.CreateClient(session.AccessToken);
            var added = await client.PostAsJsonAsync("/api/v1/me/addresses", new
            {
                label = "HOME",
                recipientName = "Test",
                phone = "+919876543210",
                line1 = "12 MG Road",
                locality = "Anna Nagar",
                cityName = "Test City",
                stateCode = "TN",
                pincode = addressPincode,
            }, Ct);
            added.StatusCode.Should().Be(HttpStatusCode.Created, await added.Content.ReadAsStringAsync(Ct));
        }

        return await QueryAsync(db => db.Set<Customer>().Where(c => c.UserId == session.UserId).Select(c => c.Id).SingleAsync(Ct));
    }

    private async Task<(Guid CityId, string Pincode)> CreateCityWithPincodeAsync()
    {
        using var admin = Factory.CreateStaffClient(["locations.manage"], allLocations: true);
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var city = await PostOkAsync(admin, "/api/v1/admin/cities", new { code = $"CITY_{suffix}", name = "Test City", stateCode = "TN", timeZone = "Asia/Kolkata" });
        var cityId = city.GetProperty("id").GetGuid();
        var location = await PostOkAsync(admin, "/api/v1/admin/fulfillment-locations", new
        {
            cityId,
            code = $"LOC-{suffix}",
            name = "Test Store",
            addressLine = "1 Beach Road",
            pincode = "600041",
            phone = "+914400000000",
            latitude = 12.98m,
            longitude = 80.26m,
            hasColdStorage = true,
            fssaiLicenseNo = "12345678901234",
        });
        var area = await PostOkAsync(admin, "/api/v1/admin/service-areas", new
        {
            cityId,
            fulfillmentLocationId = location.GetProperty("id").GetGuid(),
            name = "Area",
            deliveryFeePaise = 3900,
            freeDeliveryAbovePaise = 49900,
            minOrderPaise = 29900,
        });
        var pincode = Random.Shared.Next(700_000, 899_999).ToString(CultureInfo.InvariantCulture);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/service-areas/{area.GetProperty("id").GetGuid()}/pincodes")
        {
            Content = JsonContent.Create(new { pincodes = new[] { pincode } }),
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{area.GetProperty("version").GetString()}\""));
        var added = await admin.SendAsync(request, Ct);
        added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync(Ct));
        return (cityId, pincode);
    }

    private static async Task<JsonElement> PostOkAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, Ct);
        var json = await ReadJsonAsync(response);
        response.IsSuccessStatusCode.Should().BeTrue($"POST {url} answered {json}");
        return json;
    }

    private static async Task<JsonElement> GetOkAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        var json = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url} answered {json}");
        return json;
    }

    /// <summary>An order row with only what the G6 reads use (written directly; Ordering owns the table in production).</summary>
    private Task<Guid> InsertOrderAsync(Guid customerId, Guid locationId, string status, DateTimeOffset placedAt, long totalPaise, long? finalTotalPaise)
        => QueryAsync(async db =>
        {
            var id = Guid.CreateVersion7();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO ordering.orders (id, order_number, customer_id, cart_id, city_id, fulfillment_location_id, service_area_id, status,
                    payment_method, substitution_preference, address_id, recipient_name, phone, line1, locality, city_name, state_code, pincode,
                    delivery_slot_id, slot_start_at, slot_end_at, idempotency_key, items_paise, preparation_paise, discount_paise,
                    delivery_fee_paise, delivery_fee_tax_rate_bp, tax_paise, total_paise, max_total_paise, final_total_paise, amount_due_paise,
                    refund_due_paise, refund_due_weight_shortfall_paise, refund_due_short_supply_paise, cold_box_used, captured_paise,
                    placed_at, changed_at, created_at)
                VALUES ({id}, {"T-" + id.ToString("N")[..12]}, {customerId}, {Guid.CreateVersion7()}, {Guid.CreateVersion7()}, {locationId}, {Guid.CreateVersion7()}, {status},
                    'COD', 'CALL_ME', {Guid.CreateVersion7()}, 'Test', '+919876543210', '12 MG Road', 'Anna Nagar', 'Test City', 'TN', '600040',
                    {Guid.CreateVersion7()}, {placedAt.AddHours(4)}, {placedAt.AddHours(5)}, {id.ToString()}, {totalPaise}, 0, 0,
                    0, 0, 0, {totalPaise}, {totalPaise}, {finalTotalPaise}, {finalTotalPaise ?? totalPaise},
                    0, 0, 0, false, 0,
                    {placedAt}, {placedAt}, {placedAt})
                """, Ct);
            return id;
        });
}
