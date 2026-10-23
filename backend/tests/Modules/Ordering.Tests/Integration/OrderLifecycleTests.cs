using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.Ordering.Application.Events;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Ordering.Tests.Integration;

public sealed class OrderLifecycleTests(OrderingDatabase database) : OrderingTestBase(database)
{
    [Fact]
    public async Task Customer_cancels_a_confirmed_order_and_only_ordering_rows_change()
    {
        var orderId = await PlaceOrderAsync();

        var response = await CancelAsCustomerAsync(orderId);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        (await ReadJsonAsync(response)).GetProperty("status").GetString().Should().Be("CANCELLED");
        var cancelled = (await OutboxAsync("OrderCancelled", orderId)).Should().ContainSingle().Subject;
        cancelled.GetProperty("previousStatus").GetString().Should().Be("CONFIRMED");
        cancelled.GetProperty("deliverySlotId").GetGuid().Should().Be(World.SlotId);
        cancelled.GetProperty("stockConsumed").GetBoolean().Should().BeFalse();
        cancelled.GetProperty("actorType").GetString().Should().Be("CUSTOMER");
        (await LoadOrderAsync(orderId))!.Items.Should().OnlyContain(i => i.Status == OrderItemStatus.Cancelled);
    }

    [Fact]
    public async Task Packed_order_is_cancelled_only_by_staff_with_stock_consumed()
    {
        var orderId = await PlaceOrderAsync();
        var staff = Staff();
        await StartPreparingAsync(staff, orderId);
        (await PackAsync(staff, orderId, 500, 2)).StatusCode.Should().Be(HttpStatusCode.OK);

        var byCustomer = await CancelAsCustomerAsync(orderId);
        byCustomer.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        CodeOf(await ReadJsonAsync(byCustomer)).Should().Be("ORDER_NOT_CANCELLABLE");

        var byStaff = await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/cancel", await ETagOfAsync(staff, orderId), new { reasonCode = "QUALITY_ISSUE" });
        byStaff.StatusCode.Should().Be(HttpStatusCode.OK, await byStaff.Content.ReadAsStringAsync(Ct));
        var cancelled = (await OutboxAsync("OrderCancelled", orderId)).Should().ContainSingle().Subject;
        cancelled.GetProperty("previousStatus").GetString().Should().Be("PACKED");
        cancelled.GetProperty("stockConsumed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_after_dispatch_is_refused_for_staff()
    {
        var orderId = await PlaceOrderAsync();
        var staff = Staff();
        await StartPreparingAsync(staff, orderId);
        await PackAsync(staff, orderId, 500, 2);
        await AsHandlerAsync(s => s.GetRequiredService<OrderEventService>().OnShipmentDispatchedAsync(orderId, Guid.NewGuid(), null, Start.AddHours(4), Ct));

        var response = await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/cancel", await ETagOfAsync(staff, orderId), new { reasonCode = "OTHER" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        CodeOf(await ReadJsonAsync(response)).Should().Be("ORDER_NOT_CANCELLABLE");
    }

    [Fact]
    public async Task Pack_writes_final_totals_and_the_order_packed_payload()
    {
        var orderId = await PlaceOrderAsync();
        var staff = Staff();
        await StartPreparingAsync(staff, orderId);

        var response = await PackAsync(staff, orderId, 580, 2);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var pack = await ReadJsonAsync(response);
        pack.GetProperty("status").GetString().Should().Be("PACKED");
        var fish = pack.GetProperty("lines")[0];
        fish.GetProperty("charged").GetProperty("value").GetInt32().Should().Be(550);
        fish.GetProperty("capped").GetBoolean().Should().BeTrue();
        fish.GetProperty("finalTotalPaise").GetInt64().Should().Be(73645);
        pack.GetProperty("finalTotalPaise").GetInt64().Should().Be(203645);
        pack.GetProperty("amountDuePaise").GetInt64().Should().Be(203645);

        var packed = (await OutboxAsync("OrderPacked", orderId)).Should().ContainSingle().Subject;
        packed.GetProperty("finalTotalPaise").GetInt64().Should().Be(203645);
        var line = packed.GetProperty("lines")[0];
        line.GetProperty("packedQuantity").GetInt32().Should().Be(580);
        line.GetProperty("chargedQuantity").GetInt32().Should().Be(550);
        line.GetProperty("status").GetString().Should().Be("ACTIVE");
        packed.GetProperty("refundDueByReason").GetProperty("SHORT_SUPPLY").GetInt64().Should().Be(0);

        var order = await LoadOrderAsync(orderId);
        order!.FinalTotalPaise.Should().Be(203645);
        order.Items.First(i => i.LineNo == 1).FinalItemPaise.Should().Be(71445);
        order.Items.First(i => i.LineNo == 1).FinalPreparationPaise.Should().Be(2200);
    }

    [Fact]
    public async Task Stale_or_missing_if_match_is_refused()
    {
        var orderId = await PlaceOrderAsync();
        var staff = Staff();
        var etag = await ETagOfAsync(staff, orderId);
        await StartPreparingAsync(staff, orderId);

        var stale = await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/pack", etag, PackBody(await LoadOrderAsync(orderId), 500, 2));
        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        CodeOf(await ReadJsonAsync(stale)).Should().Be("CONCURRENCY_CONFLICT");

        var missing = await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/pack", null, PackBody(await LoadOrderAsync(orderId), 500, 2));
        missing.StatusCode.Should().Be((HttpStatusCode)428);
    }

    [Fact]
    public async Task Prepaid_short_supply_refund_is_split_by_reason_in_the_pack_commit()
    {
        var orderId = await PlaceOrderAsync("ONLINE");
        await AsHandlerAsync(s => s.GetRequiredService<OrderEventService>().OnPaymentCapturedAsync(orderId, Guid.NewGuid(), World.EstimatedTotalPaise, Start.AddMinutes(1), Ct));
        World.PaidPaise = World.EstimatedTotalPaise;
        var staff = Staff();
        await StartPreparingAsync(staff, orderId);

        var response = await PackAsync(staff, orderId, 500, 0);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var packed = (await OutboxAsync("OrderPacked", orderId)).Should().ContainSingle().Subject;
        packed.GetProperty("refundDueByReason").GetProperty("SHORT_SUPPLY").GetInt64().Should().Be(130000);
        packed.GetProperty("refundDuePaise").GetInt64().Should().Be(130000);
        packed.GetProperty("lines")[1].GetProperty("status").GetString().Should().Be("SHORT_SUPPLIED");
        packed.GetProperty("amountDuePaise").GetInt64().Should().Be(0);
    }

    [Fact]
    public async Task Expired_substitution_short_supplies_the_line_and_pack_lists_it()
    {
        var orderId = await PlaceOrderAsync();
        var staff = Staff();
        await StartPreparingAsync(staff, orderId);
        var order = await LoadOrderAsync(orderId);
        var fish = order!.Items.First(i => i.LineNo == 1);

        var propose = await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/items/{fish.Id}/substitutions", await ETagOfAsync(staff, orderId),
            new { variantId = World.SubstituteVariantId, quantity = new { value = 500, unit = "GRAM" } });
        propose.StatusCode.Should().Be(HttpStatusCode.OK, await propose.Content.ReadAsStringAsync(Ct));
        (await OutboxAsync("SubstitutionProposed", orderId)).Should().ContainSingle();

        Factory.Clock.Advance(TimeSpan.FromMinutes(14));
        (await InScopeAsync((s, _) => s.GetRequiredService<OrderExpiryService>().ExpireSubstitutionsAsync(Ct))).Should().Be(0);
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await InScopeAsync(async (s, _) =>
        {
            await s.GetRequiredService<ExpireSubstitutionsJob>().ExecuteAsync(Ct);
            return true;
        });

        var expired = await LoadOrderAsync(orderId);
        expired!.Substitutions.Should().ContainSingle().Which.Status.Should().Be(SubstitutionStatus.Expired);
        expired.Items.First(i => i.Id == fish.Id).Status.Should().Be(OrderItemStatus.ShortSupplied);
        expired.Adjustments.Should().ContainSingle(a => a.Kind == AdjustmentKind.ShortSupply && a.AmountPaise == -fish.EstimatedTotalPaise);
        (await OutboxAsync("SubstitutionResolved", orderId)).Should().ContainSingle().Which.GetProperty("status").GetString().Should().Be("EXPIRED");

        var crabs = expired.Items.First(i => i.LineNo == 2);
        var pack = await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/pack", await ETagOfAsync(staff, orderId),
            new { lines = new[] { new { orderItemId = crabs.Id, packedQuantity = new { value = 2, unit = "PIECE" } } }, coldBoxUsed = true });
        pack.StatusCode.Should().Be(HttpStatusCode.OK, await pack.Content.ReadAsStringAsync(Ct));
        var packed = (await OutboxAsync("OrderPacked", orderId)).Should().ContainSingle().Subject;
        packed.GetProperty("lines").EnumerateArray().Should().Contain(l => l.GetProperty("orderItemId").GetGuid() == fish.Id && l.GetProperty("status").GetString() == "SHORT_SUPPLIED");
    }

    [Fact]
    public async Task Customer_accepts_a_substitution_with_a_fresh_reservation()
    {
        var orderId = await PlaceOrderAsync();
        var staff = Staff();
        await StartPreparingAsync(staff, orderId);
        var fish = (await LoadOrderAsync(orderId))!.Items.First(i => i.LineNo == 1);
        await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/items/{fish.Id}/substitutions", await ETagOfAsync(staff, orderId),
            new { variantId = World.SubstituteVariantId, quantity = new { value = 500, unit = "GRAM" } });
        var substitutionId = (await LoadOrderAsync(orderId))!.Substitutions.Single().Id;

        var accept = await Customer.PostAsync(new Uri($"/api/v1/orders/{orderId}/substitutions/{substitutionId}/accept", UriKind.Relative), null, Ct);
        var again = await Customer.PostAsync(new Uri($"/api/v1/orders/{orderId}/substitutions/{substitutionId}/accept", UriKind.Relative), null, Ct);
        var opposite = await Customer.PostAsync(new Uri($"/api/v1/orders/{orderId}/substitutions/{substitutionId}/reject", UriKind.Relative), null, Ct);

        accept.StatusCode.Should().Be(HttpStatusCode.OK, await accept.Content.ReadAsStringAsync(Ct));
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        opposite.StatusCode.Should().Be(HttpStatusCode.Conflict);
        CodeOf(await ReadJsonAsync(opposite)).Should().Be("SUBSTITUTION_NOT_PENDING");
        var order = await LoadOrderAsync(orderId);
        order!.Items.Should().HaveCount(3);
        order.Items.Single(i => i.LineNo == 3).VariantId.Should().Be(World.SubstituteVariantId);
        order.Items.Single(i => i.LineNo == 1).Status.Should().Be(OrderItemStatus.Substituted);
    }

    [Fact]
    public async Task Staff_of_another_location_cannot_see_or_change_the_order()
    {
        var orderId = await PlaceOrderAsync();
        using var outsider = ClientFor(f => f.CreateStaffClient(["orders.view", "orders.pack"], [Guid.NewGuid()]));

        var detail = await outsider.GetAsync(new Uri($"/api/v1/admin/orders/{orderId}", UriKind.Relative), Ct);
        detail.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        CodeOf(await ReadJsonAsync(detail)).Should().Be("LOCATION_ACCESS_DENIED");

        // Filter by order number: the shared test database holds more orders than one page.
        var orderNumber = (await LoadOrderAsync(orderId))!.OrderNumber;
        var listUri = new Uri($"/api/v1/admin/orders?q={orderNumber}", UriKind.Relative);
        var list = await ReadJsonAsync(await outsider.GetAsync(listUri, Ct));
        list.GetProperty("items").EnumerateArray().Should().NotContain(i => i.GetProperty("id").GetGuid() == orderId);

        var insiderList = await ReadJsonAsync(await Staff().GetAsync(listUri, Ct));
        insiderList.GetProperty("items").EnumerateArray().Should().Contain(i => i.GetProperty("id").GetGuid() == orderId);
    }

    [Fact]
    public async Task Customer_sees_own_orders_only_and_pick_list_sums_gross()
    {
        var orderId = await PlaceOrderAsync();
        var mine = await Customer.GetAsync(new Uri($"/api/v1/orders/{orderId}", UriKind.Relative), Ct);
        mine.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(mine)).GetProperty("allowedActions").GetProperty("cancel").GetBoolean().Should().BeTrue();

        using var other = ClientFor(f => f.CreateCustomerClient());
        (await other.GetAsync(new Uri($"/api/v1/orders/{orderId}", UriKind.Relative), Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var pickList = await ReadJsonAsync(await Staff().GetAsync(new Uri($"/api/v1/admin/orders/pick-list?fulfillmentLocationId={CheckoutWorld.LocationId}&slotId={World.SlotId}", UriKind.Relative), Ct));
        pickList.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("grossQuantity").GetInt64()).Should().BeEquivalentTo([625L, 2L]);
    }

    private async Task<HttpResponseMessage> CancelAsCustomerAsync(Guid orderId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/orders/{orderId}/cancel") { Content = JsonContent.Create(new { reasonCode = "CHANGED_MIND" }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Customer.SendAsync(request, Ct);
    }

    private static async Task StartPreparingAsync(HttpClient staff, Guid orderId)
    {
        var response = await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/start-preparing", await ETagOfAsync(staff, orderId));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<HttpResponseMessage> PackAsync(HttpClient staff, Guid orderId, int fishGrams, int crabs)
        => await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/pack", await ETagOfAsync(staff, orderId), PackBody(await LoadOrderAsync(orderId), fishGrams, crabs));

    private static object PackBody(Order? order, int fishGrams, int crabs) => new
    {
        lines = new[]
        {
            new { orderItemId = order!.Items.Single(i => i.LineNo == 1).Id, packedQuantity = new { value = fishGrams, unit = "GRAM" } },
            new { orderItemId = order.Items.Single(i => i.LineNo == 2).Id, packedQuantity = new { value = crabs, unit = "PIECE" } },
        },
        coldBoxUsed = true,
    };
}
