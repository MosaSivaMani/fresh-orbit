using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.Delivery.Public;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FreshOrbit.Ordering.Tests.Integration;

public sealed class CheckoutTests(OrderingDatabase database) : OrderingTestBase(database)
{
    [Fact]
    public async Task Preview_reports_totals_methods_and_expected_total_without_writes()
    {
        var response = await Customer.PostAsJsonAsync("/api/v1/checkout/preview", PlaceBody(), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await ReadJsonAsync(response);
        preview.GetProperty("canPlaceOrder").GetBoolean().Should().BeTrue();
        preview.GetProperty("expectedTotalPaise").GetInt64().Should().Be(World.EstimatedTotalPaise);
        preview.GetProperty("slot").GetProperty("status").GetString().Should().Be("AVAILABLE");
        preview.GetProperty("address").GetProperty("label").GetString().Should().Be("HOME");
        preview.GetProperty("paymentMethods").EnumerateArray().Select(m => m.GetProperty("method").GetString()).Should().Equal("COD", "UPI_ON_DELIVERY", "ONLINE");
        await World.Inventory.DidNotReceive().ReserveAsync(Arg.Any<ReserveStockRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preview_reports_business_problems_as_blockers()
    {
        World.SlotRemaining = 0;
        World.Policy = World.Policy with { CodMaxOrderPaise = 1000 };

        var preview = await ReadJsonAsync(await Customer.PostAsJsonAsync("/api/v1/checkout/preview", PlaceBody(), Ct));

        preview.GetProperty("canPlaceOrder").GetBoolean().Should().BeFalse();
        preview.GetProperty("blockers").EnumerateArray().Select(b => b.GetProperty("code").GetString()).Should().BeEquivalentTo(["SLOT_FULL", "COD_LIMIT_EXCEEDED"]);
    }

    [Fact]
    public async Task Place_cod_order_reserves_books_numbers_converts_and_writes_order_placed_in_one_commit()
    {
        var response = await PlaceAsync(PlaceBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var body = await ReadJsonAsync(response);
        var orderId = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().Should().Be($"/api/v1/orders/{orderId}");
        response.Headers.ETag.Should().NotBeNull();
        body.GetProperty("status").GetString().Should().Be("CONFIRMED");
        body.GetProperty("orderNumber").GetString().Should().StartWith("FO-26");
        body.GetProperty("estimate").GetProperty("totalPaise").GetInt64().Should().Be(World.EstimatedTotalPaise);
        body.GetProperty("final").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        await World.Inventory.Received(1).ReserveAsync(
            Arg.Is<ReserveStockRequest>(r => r.OrderId == orderId && r.ExpiresAt == null && r.Lines.Count == 2 && r.Lines[0].Quantity == 625 && r.Lines[1].Quantity == 2),
            Arg.Any<CancellationToken>());
        await World.Delivery.Received(1).BookSlotAsync(World.SlotId, CheckoutWorld.LocationId, Arg.Any<CancellationToken>());
        World.MarkConvertedCalls.Should().Be(1);

        var order = await LoadOrderAsync(orderId);
        order!.Items.Should().HaveCount(2);
        order.Items.Select(i => i.GrossQuantity).Should().BeEquivalentTo([625, 2]);
        order.Items.First(i => i.LineNo == 1).MaxChargeableQuantity.Should().Be(550);

        var placed = (await OutboxAsync("OrderPlaced", orderId)).Should().ContainSingle().Subject;
        placed.GetProperty("amountDuePaise").GetInt64().Should().Be(World.EstimatedTotalPaise);
        placed.GetProperty("status").GetString().Should().Be("CONFIRMED");
        placed.GetProperty("paymentMethod").GetString().Should().Be("COD");
        var line = placed.GetProperty("lines")[0];
        line.GetProperty("stockUnit").GetString().Should().Be("GRAM");
        line.GetProperty("yieldBp").GetInt32().Should().Be(8000);
        line.GetProperty("grossQuantity").GetInt32().Should().Be(625);
        (await OutboxAsync("OrderStatusChanged", orderId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Place_order_is_refused_while_the_business_profile_lacks_what_invoicing_needs()
    {
        // BE-1 / D295: refused at checkout, not found at delivery (rule 7).
        World.Profile = World.Profile with { Gstin = null, LegalName = " " };
        var key = Guid.NewGuid().ToString();

        var response = await PlaceAsync(PlaceBody(), key);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await ReadJsonAsync(response);
        problem.GetProperty("code").GetString().Should().Be("STORE_NOT_READY");
        var errors = problem.GetProperty("errors");
        errors.EnumerateObject().Select(e => e.Name).Should().BeEquivalentTo(["businessProfile.legalName", "businessProfile.gstin"]);
        errors.GetProperty("businessProfile.gstin")[0].GetProperty("code").GetString().Should().Be("STORE_NOT_READY");
        await World.Inventory.DidNotReceive().ReserveAsync(Arg.Any<ReserveStockRequest>(), Arg.Any<CancellationToken>());
        await World.Delivery.DidNotReceive().BookSlotAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        World.MarkConvertedCalls.Should().Be(0);
        await using (var db = Database.CreateDbContext())
        {
            (await db.Set<Order>().CountAsync(o => o.CustomerId == CustomerId, Ct)).Should().Be(0);
        }

        var preview = await Customer.PostAsJsonAsync("/api/v1/checkout/preview", PlaceBody(), Ct);
        preview.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadJsonAsync(preview)).GetProperty("code").GetString().Should().Be("STORE_NOT_READY");

        // Once the owner completes the profile, the same request (new key) places the order.
        World.Profile = World.Profile with { Gstin = "33ABCDE1234F1Z5", LegalName = "Fresh Orbit Foods Pvt Ltd" };
        (await PlaceAsync(PlaceBody())).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Online_order_waits_for_payment_with_a_reservation_expiry()
    {
        var response = await PlaceAsync(PlaceBody("ONLINE"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(response);
        body.GetProperty("status").GetString().Should().Be("PENDING_PAYMENT");
        await World.Inventory.Received(1).ReserveAsync(Arg.Is<ReserveStockRequest>(r => r.ExpiresAt == Start.AddMinutes(15)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Same_key_and_body_replays_the_order_without_placing_twice()
    {
        var key = Guid.NewGuid().ToString();
        var first = await PlaceAsync(PlaceBody(), key);
        var second = await PlaceAsync(PlaceBody(), key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        second.Headers.GetValues("Idempotent-Replayed").Should().Equal("true");
        (await ReadJsonAsync(second)).GetProperty("id").GetGuid().Should().Be((await ReadJsonAsync(first)).GetProperty("id").GetGuid());
        await using var db = Database.CreateDbContext();
        (await db.Set<Order>().CountAsync(o => o.CustomerId == CustomerId, Ct)).Should().Be(1);
    }

    [Fact]
    public async Task Same_key_with_another_body_is_refused()
    {
        var key = Guid.NewGuid().ToString();
        await PlaceAsync(PlaceBody(), key);

        var reused = await PlaceAsync(PlaceBody("UPI_ON_DELIVERY"), key);

        reused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        CodeOf(await ReadJsonAsync(reused)).Should().Be("IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task Purged_idempotency_record_still_returns_the_existing_order()
    {
        var key = Guid.NewGuid().ToString();
        var first = await ReadJsonAsync(await PlaceAsync(PlaceBody(), key));
        await using (var db = Database.CreateDbContext())
        {
            await db.Set<IdempotencyRecord>().Where(r => r.Key == key).ExecuteDeleteAsync(Ct);
        }

        var again = await PlaceAsync(PlaceBody(), key);

        again.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJsonAsync(again)).GetProperty("id").GetGuid().Should().Be(first.GetProperty("id").GetGuid());
        await World.Inventory.Received(1).ReserveAsync(Arg.Any<ReserveStockRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Price_changed_returns_the_new_preview_and_writes_nothing()
    {
        var key = Guid.NewGuid().ToString();
        var stale = World.EstimatedTotalPaise - 100;

        var response = await PlaceAsync(PlaceBody(expectedTotal: stale), key);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await ReadJsonAsync(response);
        CodeOf(problem).Should().Be("PRICE_CHANGED");
        problem.GetProperty("preview").GetProperty("expectedTotalPaise").GetInt64().Should().Be(World.EstimatedTotalPaise);
        await World.Inventory.DidNotReceive().ReserveAsync(Arg.Any<ReserveStockRequest>(), Arg.Any<CancellationToken>());
        await World.Delivery.DidNotReceive().BookSlotAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        World.MarkConvertedCalls.Should().Be(0);

        var replay = await PlaceAsync(PlaceBody(expectedTotal: stale), key);
        replay.StatusCode.Should().Be(HttpStatusCode.Conflict);
        replay.Headers.GetValues("Idempotent-Replayed").Should().Equal("true");
        await using var db = Database.CreateDbContext();
        (await db.Set<Order>().CountAsync(o => o.CustomerId == CustomerId, Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Lost_races_are_retried_and_only_one_order_number_is_consumed()
    {
        long before;
        await using (var db = Database.CreateDbContext())
        {
            before = await NumberSeries.PeekAsync(db, "ORDER", SettingsSeed.Period, Ct);
        }

        World.RacesToLose = 2;

        var response = await PlaceAsync(PlaceBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        World.MarkConvertedCalls.Should().Be(3);
        await World.Inventory.Received(3).ReserveAsync(Arg.Any<ReserveStockRequest>(), Arg.Any<CancellationToken>());
        var number = (await ReadJsonAsync(response)).GetProperty("orderNumber").GetString();
        number.Should().Be($"FO-26{before:0000}");
        await using var check = Database.CreateDbContext();
        (await NumberSeries.PeekAsync(check, "ORDER", SettingsSeed.Period, Ct)).Should().Be(before + 1);
    }

    [Fact]
    public async Task Losing_every_attempt_is_a_concurrent_save_conflict_and_persists_nothing()
    {
        long before;
        await using (var db = Database.CreateDbContext())
        {
            before = await NumberSeries.PeekAsync(db, "ORDER", SettingsSeed.Period, Ct);
        }

        World.RacesToLose = 4;

        var response = await PlaceAsync(PlaceBody());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        CodeOf(await ReadJsonAsync(response)).Should().Be("CONCURRENT_SAVE");
        World.MarkConvertedCalls.Should().Be(4);
        await using var check = Database.CreateDbContext();
        (await check.Set<Order>().CountAsync(o => o.CustomerId == CustomerId, Ct)).Should().Be(0);
        (await NumberSeries.PeekAsync(check, "ORDER", SettingsSeed.Period, Ct)).Should().Be(before);
    }

    [Fact]
    public async Task Refusals_from_reservation_and_slot_booking_are_returned_with_their_codes()
    {
        World.ReserveSucceeds = false;
        var outOfStock = await PlaceAsync(PlaceBody());
        outOfStock.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        CodeOf(await ReadJsonAsync(outOfStock)).Should().Be("OUT_OF_STOCK");

        World.ReserveSucceeds = true;
        World.Delivery.BookSlotAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new SlotBooking(false, ci.ArgAt<Guid>(0), null, null, "SLOT_FULL"));
        var full = await PlaceAsync(PlaceBody());
        full.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        CodeOf(await ReadJsonAsync(full)).Should().Be("SLOT_FULL");
        await using var db = Database.CreateDbContext();
        (await db.Set<Order>().CountAsync(o => o.CustomerId == CustomerId, Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Coupon_is_redeemed_and_its_allocation_stored_on_the_lines()
    {
        World.CouponCode = "FRESH100";
        World.CouponDiscountPaise = 10000;

        var orderId = await PlaceOrderAsync();

        var order = await LoadOrderAsync(orderId);
        order!.DiscountPaise.Should().Be(10000);
        order.CouponCode.Should().Be("FRESH100");
        order.Items.Sum(i => i.DiscountPaise).Should().Be(10000);
        await World.Promotions.Received(1).RedeemAsync(Arg.Is<Promotions.Public.RedeemCouponRequest>(r => r.OrderId == orderId && r.Code == "FRESH100"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_needs_a_customer_login()
    {
        using var anonymous = Factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/v1/checkout/preview", PlaceBody(), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        CodeOf(await ReadJsonAsync(response)).Should().Be("LOGIN_REQUIRED");
    }

    [Fact]
    public async Task Empty_cart_and_missing_key_are_refused()
    {
        var noKey = await Customer.PostAsJsonAsync("/api/v1/orders", PlaceBody(), Ct);
        noKey.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        CodeOf(await ReadJsonAsync(noKey)).Should().Be("IDEMPOTENCY_KEY_INVALID");

        World.Lines = [];
        var empty = await PlaceAsync(PlaceBody(expectedTotal: 0));
        empty.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        CodeOf(await ReadJsonAsync(empty)).Should().Be("CART_EMPTY");
    }
}
