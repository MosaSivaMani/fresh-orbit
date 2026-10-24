using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Delivery.Application.Handlers;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Public;
using FreshOrbit.Delivery.Tests.Support;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Delivery.Tests.Integration;

/// <summary>Admin and customer endpoints through the real Api pipeline.</summary>
public sealed class DeliveryApiTests(DeliveryDatabase database) : IAsyncLifetime
{
    private const string Admin = "/api/v1/admin/delivery";
    private readonly DeliveryApiFactory _factory = new(database);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task I9_happy_path_assign_dispatch_deliver_with_cash_and_outbox_events()
    {
        var location = _factory.Locations.AddLocation();
        var client = Staff(location.Id);
        var (shipmentId, orderId) = await PlacedAndPackedShipmentAsync(location.Id);
        var riderId = await CreateRiderAsync(client, location.Id);

        var assigned = await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/assign", new { riderId }, await EtagAsync(client, shipmentId));
        assigned.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(assigned)).GetProperty("status").GetString().Should().Be("ASSIGNED");

        var dispatched = await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/dispatch", new { dispatchTemperatureC = 5.2m }, assigned.Headers.ETag!.Tag);
        dispatched.StatusCode.Should().Be(HttpStatusCode.OK);
        var dispatchedBody = await JsonAsync(dispatched);
        dispatchedBody.GetProperty("status").GetString().Should().Be("OUT_FOR_DELIVERY");
        dispatchedBody.GetProperty("temperatureWarning").GetBoolean().Should().BeTrue();

        var tracking = await InScopeAsync(sp => sp.GetRequiredService<IDeliveryApi>().GetShipmentForOrderAsync(orderId, Ct));
        tracking!.DeliveryCode.Should().MatchRegex("^[0-9]{4}$");

        var delivered = await SendAsync(
            client,
            HttpMethod.Post,
            $"{Admin}/shipments/{shipmentId}/deliver",
            new { deliveryCode = tracking.DeliveryCode, cashCollectedPaise = 66950, cashMode = "CASH" },
            dispatched.Headers.ETag!.Tag);
        delivered.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await JsonAsync(delivered);
        body.GetProperty("status").GetString().Should().Be("DELIVERED");
        body.GetProperty("cashCollectedPaise").GetInt64().Should().Be(66950);
        body.GetProperty("attempts")[0].GetProperty("outcome").GetString().Should().Be("DELIVERED");

        (await OutboxEventsAsync(shipmentId)).Should().Equal("RiderAssigned", "ShipmentDispatched", "ShipmentDelivered");
        (await InScopeAsync(sp => sp.GetRequiredService<IDeliveryApi>().GetShipmentForOrderAsync(orderId, Ct)))!.DeliveryCode.Should().BeNull();
    }

    [Fact]
    public async Task I10_failed_attempt_reattempt_then_deliver_numbers_attempts()
    {
        var location = _factory.Locations.AddLocation();
        var client = Staff(location.Id);
        var (shipmentId, orderId) = await DispatchedShipmentAsync(client, location.Id);

        var failed = await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/attempts", new { outcome = "CUSTOMER_UNAVAILABLE" }, await EtagAsync(client, shipmentId));
        failed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(failed)).GetProperty("status").GetString().Should().Be("FAILED");

        var reattempt = await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/reattempt", null, failed.Headers.ETag!.Tag);
        reattempt.StatusCode.Should().Be(HttpStatusCode.OK);

        var code = (await InScopeAsync(sp => sp.GetRequiredService<IDeliveryApi>().GetShipmentForOrderAsync(orderId, Ct)))!.DeliveryCode;
        var delivered = await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/deliver", new { deliveryCode = code }, reattempt.Headers.ETag!.Tag);

        delivered.StatusCode.Should().Be(HttpStatusCode.OK);
        var attempts = (await JsonAsync(delivered)).GetProperty("attempts").EnumerateArray()
            .Select(a => (a.GetProperty("attemptNo").GetInt32(), a.GetProperty("outcome").GetString())).ToList();
        attempts.Should().Equal((1, "CUSTOMER_UNAVAILABLE"), (2, "DELIVERED"));
        (await OutboxEventsAsync(shipmentId)).Should().EndWith(["DeliveryAttemptFailed", "ShipmentReattempted", "ShipmentDelivered"]);
    }

    [Fact]
    public async Task Five_wrong_delivery_codes_lock_the_shipment()
    {
        var location = _factory.Locations.AddLocation();
        var client = Staff(location.Id);
        var (shipmentId, orderId) = await DispatchedShipmentAsync(client, location.Id);
        var code = (await InScopeAsync(sp => sp.GetRequiredService<IDeliveryApi>().GetShipmentForOrderAsync(orderId, Ct)))!.DeliveryCode!;
        var wrong = code == "0000" ? "1111" : "0000";

        var codes = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            var response = await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/deliver", new { deliveryCode = i < 5 ? wrong : code }, await EtagAsync(client, shipmentId));
            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            codes.Add((await JsonAsync(response)).GetProperty("code").GetString()!);
        }

        codes.Should().Equal(Enumerable.Repeat("DELIVERY_CODE_INVALID", 5).Append("DELIVERY_CODE_LOCKED"));
        var shipment = await JsonAsync(await client.GetAsync($"{Admin}/shipments/{shipmentId}", Ct));
        shipment.GetProperty("deliveryCodeFailures").GetInt32().Should().Be(5);
        shipment.GetProperty("deliveryCodeLocked").GetBoolean().Should().BeTrue();
        shipment.GetProperty("status").GetString().Should().Be("OUT_FOR_DELIVERY");
    }

    [Fact]
    public async Task I11_dispatch_with_stale_or_missing_If_Match()
    {
        var location = _factory.Locations.AddLocation();
        var client = Staff(location.Id);
        var (shipmentId, _) = await PlacedAndPackedShipmentAsync(location.Id);
        var riderId = await CreateRiderAsync(client, location.Id);
        var etag = await EtagAsync(client, shipmentId);
        (await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/assign", new { riderId }, etag)).StatusCode.Should().Be(HttpStatusCode.OK);

        var stale = await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/dispatch", new { dispatchTemperatureC = 2.0m }, etag);
        var missing = await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/dispatch", new { dispatchTemperatureC = 2.0m }, null);

        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        (await JsonAsync(stale)).GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");
        missing.StatusCode.Should().Be((HttpStatusCode)428);
        (await JsonAsync(missing)).GetProperty("code").GetString().Should().Be("VERSION_REQUIRED");
    }

    [Fact]
    public async Task I14_staff_scoped_to_another_location_gets_403_and_filtered_lists()
    {
        var mine = _factory.Locations.AddLocation();
        var other = _factory.Locations.AddLocation();
        var (shipmentId, _) = await PlacedAndPackedShipmentAsync(other.Id);
        await PlacedAndPackedShipmentAsync(mine.Id);
        var client = Staff(mine.Id);

        var assign = await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/assign", new { riderId = Guid.CreateVersion7() }, "\"1\"");
        var list = await JsonAsync(await client.GetAsync($"{Admin}/shipments?fulfillmentLocationId={other.Id}", Ct));
        var createRider = await client.PostAsJsonAsync($"{Admin}/riders", new { fulfillmentLocationId = other.Id, fullName = "Ravi", phone = UniquePhone() }, Ct);

        assign.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await JsonAsync(assign)).GetProperty("code").GetString().Should().Be("LOCATION_ACCESS_DENIED");
        list.GetProperty("totalCount").GetInt32().Should().Be(0);
        createRider.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Missing_permission_is_403()
    {
        var client = _factory.CreateStaffClient(["orders.view"], allLocations: true);

        (await client.GetAsync($"{Admin}/shipments", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Template_overlap_is_409_and_the_database_constraint_backs_it_up()
    {
        var location = _factory.Locations.AddLocation();
        var client = Staff(location.Id);

        var first = await client.PostAsJsonAsync($"{Admin}/slot-templates", Template(location.Id, "08:00", "10:00"), Ct);
        var overlap = await client.PostAsJsonAsync($"{Admin}/slot-templates", Template(location.Id, "09:30", "11:00"), Ct);
        var adjacent = await client.PostAsJsonAsync($"{Admin}/slot-templates", Template(location.Id, "10:00", "12:00"), Ct);
        var invalid = await client.PostAsJsonAsync($"{Admin}/slot-templates", Template(location.Id, "10:10", "09:00"), Ct);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        first.Headers.ETag.Should().NotBeNull();
        overlap.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await JsonAsync(overlap)).GetProperty("code").GetString().Should().Be("SLOT_TEMPLATE_OVERLAP");
        adjacent.StatusCode.Should().Be(HttpStatusCode.Created);
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await JsonAsync(invalid)).GetProperty("errors").TryGetProperty("startTime", out _).Should().BeTrue();

        // Backstop: an overlapping row inserted behind the service's back violates the EXCLUDE constraint.
        await using var db = database.CreateDbContext();
        var act = () => db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO delivery.slot_templates (id, fulfillment_location_id, day_of_week, start_time, end_time, capacity, cutoff_minutes, is_active, created_at)
            VALUES ({Guid.CreateVersion7()}, {location.Id}, 1, TIME '09:00', TIME '09:30', 5, 0, TRUE, now())
            """,
            Ct);
        (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which.SqlState.Should().Be("23P01");
    }

    [Fact]
    public async Task Slot_patch_refuses_capacity_below_booked_and_closes_with_alert()
    {
        var location = _factory.Locations.AddLocation();
        var client = Staff(location.Id);
        var slotId = await SeedSlotAsync(location.Id, DateTimeOffset.UtcNow.AddHours(8), booked: 2);
        var list = await JsonAsync(await client.GetAsync($"{Admin}/slots?fulfillmentLocationId={location.Id}", Ct));
        var version = list.GetProperty("items")[0].GetProperty("version").GetString();

        var below = await SendAsync(client, HttpMethod.Patch, $"{Admin}/slots/{slotId}", new { capacity = 1 }, $"\"{version}\"");
        var closed = await SendAsync(client, HttpMethod.Patch, $"{Admin}/slots/{slotId}", new { status = "CLOSED" }, $"\"{version}\"");

        below.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await JsonAsync(below)).GetProperty("code").GetString().Should().Be("SLOT_CAPACITY_BELOW_BOOKED");
        closed.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await JsonAsync(closed);
        body.GetProperty("status").GetString().Should().Be("CLOSED");
        body.GetProperty("closedReason").GetString().Should().Be("MANUAL");
        (await OutboxEventsAsync(slotId)).Should().Equal("SlotClosedWithBookings");
    }

    [Fact]
    public async Task Rider_phone_must_be_unique_among_active_riders_and_busy_rider_cannot_be_deactivated()
    {
        var location = _factory.Locations.AddLocation();
        var client = Staff(location.Id);
        var phone = UniquePhone();
        var (shipmentId, _) = await PlacedAndPackedShipmentAsync(location.Id);

        var first = await client.PostAsJsonAsync($"{Admin}/riders", new { fulfillmentLocationId = location.Id, fullName = "Ravi", phone }, Ct);
        var duplicate = await client.PostAsJsonAsync($"{Admin}/riders", new { fulfillmentLocationId = location.Id, fullName = "Kumar", phone }, Ct);
        var badPhone = await client.PostAsJsonAsync($"{Admin}/riders", new { fulfillmentLocationId = location.Id, fullName = "Kumar", phone = "98765" }, Ct);
        var riderId = (await JsonAsync(first)).GetProperty("id").GetGuid();
        (await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/assign", new { riderId }, await EtagAsync(client, shipmentId))).StatusCode.Should().Be(HttpStatusCode.OK);
        var deactivate = await SendAsync(client, HttpMethod.Put, $"{Admin}/riders/{riderId}", new { fullName = "Ravi", phone, isActive = false }, first.Headers.ETag!.Tag);

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await JsonAsync(duplicate)).GetProperty("code").GetString().Should().Be("DUPLICATE");
        badPhone.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        deactivate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await JsonAsync(deactivate)).GetProperty("code").GetString().Should().Be("RIDER_HAS_ACTIVE_SHIPMENTS");
    }

    [Fact]
    public async Task I13_customer_delivery_slots_show_seven_days_with_displayed_status()
    {
        var location = _factory.Locations.AddLocation();
        var other = _factory.Locations.AddLocation();
        var pincode = Random.Shared.Next(100000, 999999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _factory.Locations.AddPincode(pincode, location.Id);
        var now = DateTimeOffset.UtcNow;
        var available = await SeedSlotAsync(location.Id, now.AddHours(30));
        var full = await SeedSlotAsync(location.Id, now.AddHours(40), capacity: 1, booked: 1);
        var cutoff = await SeedSlotAsync(location.Id, now.AddMinutes(60), cutoffMinutes: 120);
        var beyond = await SeedSlotAsync(location.Id, now.AddDays(9));
        await SeedSlotAsync(other.Id, now.AddHours(30));
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/v1/delivery-slots?pincode={pincode}", Ct);
        var missing = await anonymous.GetAsync("/api/v1/delivery-slots", Ct);
        var unknown = await anonymous.GetAsync("/api/v1/delivery-slots?pincode=000001", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await JsonAsync(response)).GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("id").GetGuid(), i => i.GetProperty("status").GetString());
        items.Should().HaveCount(3);
        items[available].Should().Be("AVAILABLE");
        items[full].Should().Be("FULL");
        items[cutoff].Should().Be("CUTOFF_PASSED");
        items.Should().NotContainKey(beyond);
        (await JsonAsync(missing)).GetProperty("code").GetString().Should().Be("LOCATION_REQUIRED");
        (await JsonAsync(unknown)).GetProperty("code").GetString().Should().Be("PINCODE_NOT_SERVICEABLE");
    }

    [Fact]
    public async Task G7_cash_settle_staff_look_up_riders_of_their_locations_with_the_phone_tail_only()
    {
        var location = _factory.Locations.AddLocation();
        var other = _factory.Locations.AddLocation();
        var manager = Staff(location.Id);
        var phone = UniquePhone();
        var created = await manager.PostAsJsonAsync($"{Admin}/riders", new { fulfillmentLocationId = location.Id, fullName = "Selvam K", phone }, Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var riderId = (await JsonAsync(created)).GetProperty("id").GetGuid();
        using var cashier = _factory.CreateStaffClient(["cash.settle"], [location.Id]);

        var lookup = await cashier.GetAsync($"{Admin}/riders/lookup?fulfillmentLocationId={location.Id}&q=selv", Ct);

        lookup.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await JsonAsync(lookup);
        page.GetProperty("page").GetInt32().Should().Be(1);
        var row = page.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        row.GetProperty("id").GetGuid().Should().Be(riderId);
        row.GetProperty("fullName").GetString().Should().Be("Selvam K");
        row.GetProperty("phoneLast4").GetString().Should().Be(phone[^4..]);
        row.TryGetProperty("phone", out _).Should().BeFalse();

        (await cashier.GetAsync($"{Admin}/riders", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var outside = await cashier.GetAsync($"{Admin}/riders/lookup?fulfillmentLocationId={other.Id}", Ct);
        outside.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await JsonAsync(outside)).GetProperty("code").GetString().Should().Be("LOCATION_ACCESS_DENIED");
        var scoped = await JsonAsync(await cashier.GetAsync($"{Admin}/riders/lookup?pageSize=100&q=selvam", Ct));
        scoped.GetProperty("items").EnumerateArray().Should().OnlyContain(i => i.GetProperty("fulfillmentLocationId").GetGuid() == location.Id);
        using var viewer = _factory.CreateStaffClient(["orders.view"], [location.Id]);
        (await viewer.GetAsync($"{Admin}/riders/lookup", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.GetAsync($"{Admin}/riders/lookup?fulfillmentLocationId={location.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task G10_shipments_filter_by_slot_date_and_sort_by_known_keys_only()
    {
        var location = _factory.Locations.AddLocation();
        var client = Staff(location.Id);
        var now = DateTimeOffset.UtcNow;
        var lateSlotDate = IstDate(now.AddDays(3));
        var earlySlotDate = IstDate(now.AddDays(1));
        var lateSlot = await SeedSlotAsync(location.Id, now.AddDays(3), booked: 1, slotDate: lateSlotDate);
        var earlySlot = await SeedSlotAsync(location.Id, now.AddDays(1), booked: 2, slotDate: earlySlotDate);
        var first = await ShipmentOnSlotAsync(location.Id, lateSlot, now);
        var second = await ShipmentOnSlotAsync(location.Id, earlySlot, now.AddMinutes(1));
        var third = await ShipmentOnSlotAsync(location.Id, earlySlot, now.AddMinutes(2));
        var list = $"{Admin}/shipments?fulfillmentLocationId={location.Id}&pageSize=100";

        (await IdsAsync(client, $"{list}&slotDate={earlySlotDate:yyyy-MM-dd}")).Should().Equal(second, third);
        (await IdsAsync(client, list)).Should().Equal(first, second, third);
        (await IdsAsync(client, $"{list}&sort=-placedAt")).Should().Equal(third, second, first);
        (await IdsAsync(client, $"{list}&sort=slotStartAt,-placedAt")).Should().Equal(third, second, first);
        (await IdsAsync(client, $"{list}&sort=-slotStartAt")).Should().Equal(first, second, third);

        foreach (var bad in new[] { "price", "status,status", "-" })
        {
            var refused = await client.GetAsync($"{list}&sort={bad}", Ct);
            refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, bad);
            var problem = await JsonAsync(refused);
            problem.GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
            problem.GetProperty("errors").TryGetProperty("sort", out _).Should().BeTrue();
        }
    }

    private static async Task<List<Guid>> IdsAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return [.. (await JsonAsync(response)).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
    }

    private async Task<Guid> ShipmentOnSlotAsync(Guid locationId, Guid slotId, DateTimeOffset placedAt)
    {
        var orderId = Guid.CreateVersion7();
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<OrderEventHandlers>().OnOrderPlacedAsync(orderId, locationId, slotId, placedAt, Ct);
            return await sp.GetRequiredService<FreshOrbitDbContext>().SaveChangesAsync(Ct);
        });
        return (await InScopeAsync(sp => sp.GetRequiredService<IDeliveryApi>().GetShipmentForOrderAsync(orderId, Ct)))!.ShipmentId;
    }

    private static DateOnly IstDate(DateTimeOffset instant) => DateOnly.FromDateTime(instant.AddHours(5.5).UtcDateTime);

    private HttpClient Staff(Guid locationId) => _factory.CreateStaffClient(["delivery.manage"], [locationId]);

    private static object Template(Guid locationId, string start, string end)
        => new { fulfillmentLocationId = locationId, dayOfWeek = 1, startTime = start, endTime = end, capacity = 10, cutoffMinutes = 120 };

    private static string UniquePhone() => "+9198" + Random.Shared.Next(10000000, 99999999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<Guid> CreateRiderAsync(HttpClient client, Guid locationId)
    {
        var response = await client.PostAsJsonAsync($"{Admin}/riders", new { fulfillmentLocationId = locationId, fullName = "Ravi", phone = UniquePhone() }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await JsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<(Guid ShipmentId, Guid OrderId)> PlacedAndPackedShipmentAsync(Guid locationId)
    {
        // Reattempts need "today" (IST) to be the slot's local date.
        var slotId = await SeedSlotAsync(locationId, DateTimeOffset.UtcNow.AddHours(6), booked: 1, slotDate: IstDate(DateTimeOffset.UtcNow));
        var orderId = Guid.CreateVersion7();
        await InScopeAsync(async sp =>
        {
            var handlers = sp.GetRequiredService<OrderEventHandlers>();
            await handlers.OnOrderPlacedAsync(orderId, locationId, slotId, DateTimeOffset.UtcNow, Ct);
            await sp.GetRequiredService<FreshOrbitDbContext>().SaveChangesAsync(Ct);
            await handlers.OnOrderPackedAsync(orderId, DateTimeOffset.UtcNow, true, Ct);
            return await sp.GetRequiredService<FreshOrbitDbContext>().SaveChangesAsync(Ct);
        });
        var shipment = await InScopeAsync(sp => sp.GetRequiredService<IDeliveryApi>().GetShipmentForOrderAsync(orderId, Ct));
        return (shipment!.ShipmentId, orderId);
    }

    private async Task<(Guid ShipmentId, Guid OrderId)> DispatchedShipmentAsync(HttpClient client, Guid locationId)
    {
        var (shipmentId, orderId) = await PlacedAndPackedShipmentAsync(locationId);
        var riderId = await CreateRiderAsync(client, locationId);
        var assigned = await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/assign", new { riderId }, await EtagAsync(client, shipmentId));
        var dispatched = await SendAsync(client, HttpMethod.Post, $"{Admin}/shipments/{shipmentId}/dispatch", new { dispatchTemperatureC = 2.0m }, assigned.Headers.ETag!.Tag);
        dispatched.StatusCode.Should().Be(HttpStatusCode.OK);
        return (shipmentId, orderId);
    }

    private async Task<Guid> SeedSlotAsync(Guid locationId, DateTimeOffset startAt, int capacity = 10, int booked = 0, int cutoffMinutes = 30, DateOnly? slotDate = null)
    {
        var slot = DeliverySlot.Create(locationId, null, slotDate ?? IstDate(startAt), startAt, startAt.AddHours(2), startAt.AddMinutes(-cutoffMinutes), capacity);
        for (var i = 0; i < booked; i++)
        {
            slot.Book(locationId, startAt.AddDays(-1));
        }

        await using var db = database.CreateDbContext();
        db.Add(slot);
        await db.SaveChangesAsync(Ct);
        return slot.Id;
    }

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private static async Task<string> EtagAsync(HttpClient client, Guid shipmentId)
    {
        var response = await client.GetAsync($"{Admin}/shipments/{shipmentId}", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return response.Headers.ETag!.Tag;
    }

    private async Task<List<string>> OutboxEventsAsync(Guid id)
    {
        await using var db = database.CreateDbContext();
        var rows = await db.Database
            .SqlQuery<string>($"SELECT event_type AS \"Value\" FROM platform.outbox WHERE payload::text LIKE {"%" + id + "%"} ORDER BY occurred_at, id")
            .ToListAsync(Ct);
        return [.. rows.Select(t => t.Split(',')[0].Split('.')[^1])];
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, object? body, string? ifMatch)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).Clone();
}
