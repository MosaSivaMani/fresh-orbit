using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.Ordering.Public;
using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Handlers;
using FreshOrbit.Payments.Application.Reconciliation;
using FreshOrbit.Payments.Domain.Reconciliation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OrderingPaymentMethod = FreshOrbit.Ordering.Public.PaymentMethod;
using PaymentStatus = FreshOrbit.Payments.Domain.Payments.PaymentStatus;

namespace FreshOrbit.Payments.Tests.Integration;

public sealed class CashAndReconciliationTests(PaymentsDatabase database) : PaymentsTestBase(database)
{
    private static readonly TimeZoneInfo Kolkata = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    [Fact]
    public async Task I4_cod_collection_follows_the_weight_adjusted_amount()
    {
        var (order, shipmentId, _, locationId) = await GivenCodOutForDeliveryAsync(245051, 234900);
        var rider = Factory.CreateStaffClient(["delivery.manage"], [locationId]);

        var placed = await LoadOrderPaymentsAsync(order.OrderId);
        placed.Should().ContainSingle().Which.AmountPaise.Should().Be(234900);

        var wrong = await CashCollectedAsync(rider, shipmentId, 245051, Guid.NewGuid().ToString());
        wrong.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadJsonAsync(wrong)).GetProperty("errors").EnumerateObject().SelectMany(f => f.Value.EnumerateArray())
            .Should().Contain(e => e.GetProperty("code").GetString() == "CASH_AMOUNT_MISMATCH");

        var key = Guid.NewGuid().ToString();
        var collected = await CashCollectedAsync(rider, shipmentId, 234900, key);
        collected.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(collected)).GetProperty("status").GetString().Should().Be("COLLECTED");

        var replay = await CashCollectedAsync(rider, shipmentId, 234900, key);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await CashCollectedAsync(rider, shipmentId, 234900, Guid.NewGuid().ToString());
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(again)).Should().Be("CASH_ALREADY_COLLECTED");

        var payment = (await LoadOrderPaymentsAsync(order.OrderId)).Single();
        payment.Status.Should().Be(PaymentStatus.Collected);
        payment.CollectedPaise.Should().Be(234900);
        (await CountOutboxAsync("PaymentCollected", payment.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Cash_collection_is_scoped_to_the_shipment_location()
    {
        var (_, shipmentId, _, _) = await GivenCodOutForDeliveryAsync(1000, 1000);
        var elsewhere = Factory.CreateStaffClient(["delivery.manage"], [Guid.CreateVersion7()]);

        var response = await CashCollectedAsync(elsewhere, shipmentId, 1000, Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeAsync(response)).Should().Be("LOCATION_ACCESS_DENIED");
    }

    [Fact]
    public async Task I12_settlement_with_a_difference_stays_open_and_needs_another_person()
    {
        var (_, shipmentId, riderId, locationId) = await GivenCodOutForDeliveryAsync(234900, 234900);
        (await CashCollectedAsync(Factory.CreateStaffClient(["delivery.manage"], [locationId]), shipmentId, 234900, Guid.NewGuid().ToString())).EnsureSuccessStatusCode();
        Factory.Shipments.GetRiderAsync(riderId, Arg.Any<CancellationToken>()).Returns(new RiderFacts(riderId, locationId, "Asia/Kolkata", true));
        var recorderId = Guid.CreateVersion7();
        var recorder = Factory.CreateStaffClient(["cash.settle", "payments.view"], [locationId], userId: recorderId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Factory.Clock.UtcNow, Kolkata).DateTime);

        var created = await CreateSettlementAsync(recorder, new { riderId, date = today, receivedPaise = 234000 });

        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var body = await ReadJsonAsync(created);
        body.GetProperty("expectedPaise").GetInt64().Should().Be(234900);
        body.GetProperty("differencePaise").GetInt64().Should().Be(-900);
        body.GetProperty("status").GetString().Should().Be("OPEN");
        body.GetProperty("collectionsCount").GetInt32().Should().Be(1);
        var settlementId = body.GetProperty("id").GetGuid();
        await using (var db = Database.CreateDbContext())
        {
            (await db.Set<ReconciliationIssue>().SingleAsync(i => i.SubjectId == settlementId, Ct)).Kind.Should().Be(IssueKind.CashDifference);
        }

        var duplicate = await CreateSettlementAsync(recorder, new { riderId, date = today, receivedPaise = 234900 });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(duplicate)).Should().Be("SETTLEMENT_EXISTS");

        var future = await CreateSettlementAsync(recorder, new { riderId, date = today.AddDays(2), receivedPaise = 0 });
        future.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var etag = created.Headers.ETag!.Tag;
        var self = await PostWithVersionAsync(recorder, $"/api/v1/admin/cash-settlements/{settlementId}/settle", etag, new { note = "Counted again" });
        self.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeAsync(self)).Should().Be("SETTLEMENT_SELF_RESOLUTION");

        var manager = Factory.CreateStaffClient(["cash.settle"], [locationId]);
        var settled = await PostWithVersionAsync(manager, $"/api/v1/admin/cash-settlements/{settlementId}/settle", etag, new { note = "Rider paid the rest" });
        settled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(settled)).GetProperty("status").GetString().Should().Be("SETTLED");
    }

    [Fact]
    public async Task I9_reconciliation_captures_a_payment_whose_webhook_was_missed()
    {
        var customerId = Guid.CreateVersion7();
        var order = GivenOrder(OrderStatus.PendingPayment, OrderingPaymentMethod.Online, 47950, customerId);
        var initiated = await InitiateAsync(Factory.CreateCustomerClient(customerId: customerId), order.OrderId);
        var paymentId = initiated.GetProperty("paymentId").GetGuid();
        Factory.Gateway.Capture(initiated.GetProperty("providerOrderId").GetString()!, 47950);
        Factory.Clock.Advance(TimeSpan.FromMinutes(20));

        var first = await RunReconciliationAsync();
        var second = await RunReconciliationAsync();

        first.PaymentsCaptured.Should().BeGreaterThanOrEqualTo(1);
        (await LoadPaymentAsync(paymentId)).Status.Should().Be(PaymentStatus.Captured);
        (await CountOutboxAsync("PaymentCaptured", paymentId)).Should().Be(1);
        await using var db = Database.CreateDbContext();
        var issue = await db.Set<ReconciliationIssue>().SingleAsync(i => i.SubjectId == paymentId, Ct);
        issue.Kind.Should().Be(IssueKind.MissedWebhook);
        issue.Status.Should().Be(IssueStatus.AutoResolved);
        second.Should().NotBeNull();
    }

    [Fact]
    public async Task I10_reconciliation_amount_mismatch_opens_one_issue_and_leaves_the_payment_pending()
    {
        var customerId = Guid.CreateVersion7();
        var locationId = Guid.CreateVersion7();
        var order = GivenOrder(OrderStatus.PendingPayment, OrderingPaymentMethod.Online, 66950, customerId, locationId);
        var initiated = await InitiateAsync(Factory.CreateCustomerClient(customerId: customerId), order.OrderId);
        var paymentId = initiated.GetProperty("paymentId").GetGuid();
        Factory.Gateway.Capture(initiated.GetProperty("providerOrderId").GetString()!, 70000);
        var scopedStaff = Factory.CreateStaffClient(["payments.view"], [locationId]);
        var otherStaff = Factory.CreateStaffClient(["payments.view"], [Guid.CreateVersion7()]);
        Factory.Clock.Advance(TimeSpan.FromMinutes(20));

        await RunReconciliationAsync();
        await RunReconciliationAsync();

        (await LoadPaymentAsync(paymentId)).Status.Should().Be(PaymentStatus.Pending);
        await using (var db = Database.CreateDbContext())
        {
            var issue = await db.Set<ReconciliationIssue>().SingleAsync(i => i.SubjectId == paymentId, Ct);
            issue.Kind.Should().Be(IssueKind.AmountMismatch);
            issue.Status.Should().Be(IssueStatus.Open);
            issue.FulfillmentLocationId.Should().Be(locationId);
        }

        var scoped = await scopedStaff.GetAsync("/api/v1/admin/payment-issues?kind=AMOUNT_MISMATCH", Ct);
        scoped.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(scoped)).GetProperty("items").EnumerateArray().Should().ContainSingle(i => i.GetProperty("subjectId").GetGuid() == paymentId);
        var other = await otherStaff.GetAsync("/api/v1/admin/payment-issues", Ct);
        (await ReadJsonAsync(other)).GetProperty("items").EnumerateArray().Should().NotContain(i => i.GetProperty("subjectId").GetGuid() == paymentId);
    }

    [Fact]
    public async Task Delivered_cod_with_a_different_cash_amount_raises_a_collection_difference()
    {
        var (order, shipmentId, riderId, _) = await GivenCodOutForDeliveryAsync(10000, 10000);
        var input = new ShipmentDeliveredInput(order.OrderId, shipmentId, riderId, Factory.Clock.UtcNow, 9000, "CASH", null);

        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnShipmentDeliveredAsync(input, Ct));
        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnShipmentDeliveredAsync(input, Ct));

        var payment = (await LoadOrderPaymentsAsync(order.OrderId)).Single();
        payment.Status.Should().Be(PaymentStatus.Collected);
        payment.CollectedPaise.Should().Be(9000);
        await using var db = Database.CreateDbContext();
        (await db.Set<ReconciliationIssue>().CountAsync(i => i.SubjectId == payment.Id && i.Kind == IssueKind.CollectionDifference, Ct)).Should().Be(1);
    }

    [Fact]
    public async Task I13_payment_lists_and_details_are_scoped_to_the_staff_locations()
    {
        var mine = Guid.CreateVersion7();
        var theirs = Guid.CreateVersion7();
        var (_, minePayment) = await GivenCapturedOnlineOrderAsync(1000, mine);
        var (_, theirPayment) = await GivenCapturedOnlineOrderAsync(2000, theirs);
        var staff = Factory.CreateStaffClient(["payments.view"], [mine]);

        var list = await staff.GetAsync("/api/v1/admin/payments?pageSize=100", Ct);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await ReadJsonAsync(list)).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        ids.Should().Contain(minePayment).And.NotContain(theirPayment);

        (await staff.GetAsync($"/api/v1/admin/payments/{minePayment}", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        var denied = await staff.GetAsync($"/api/v1/admin/payments/{theirPayment}", Ct);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeAsync(denied)).Should().Be("LOCATION_ACCESS_DENIED");
        (await staff.GetAsync($"/api/v1/admin/payments?fulfillmentLocationId={theirs}", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.GetAsync("/api/v1/admin/webhook-events", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var all = Factory.CreateStaffClient(["payments.view"], allLocations: true);
        var everything = await all.GetAsync($"/api/v1/admin/payments?fulfillmentLocationId={theirs}", Ct);
        (await ReadJsonAsync(everything)).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Contain(theirPayment);

        (await Factory.CreateStaffClient(["refunds.request"], [mine]).GetAsync("/api/v1/admin/payments", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(OrderSummary Order, Guid ShipmentId, Guid RiderId, Guid LocationId)> GivenCodOutForDeliveryAsync(long placedPaise, long packedPaise)
    {
        var locationId = Guid.CreateVersion7();
        var order = GivenOrder(OrderStatus.OutForDelivery, OrderingPaymentMethod.Cod, packedPaise, locationId: locationId);
        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnOrderPlacedAsync(new OrderPlacedInput(order.OrderId, order.CustomerId, locationId, "COD", placedPaise), Ct));
        if (packedPaise != placedPaise)
        {
            await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnOrderPackedAsync(
                new OrderPackedInput(order.OrderId, "COD", packedPaise, placedPaise - packedPaise, new Dictionary<string, long>(StringComparer.Ordinal)), Ct));
        }

        var shipmentId = Guid.CreateVersion7();
        var riderId = Guid.CreateVersion7();
        Factory.Shipments.GetShipmentAsync(shipmentId, Arg.Any<CancellationToken>()).Returns(new ShipmentFacts(shipmentId, order.OrderId, riderId, locationId));
        return (order, shipmentId, riderId, locationId);
    }

    private async Task<ReconciliationResult> RunReconciliationAsync()
        => await InScopeAsync(sp => sp.GetRequiredService<ReconciliationService>().RunAsync(Ct));

    private static async Task<HttpResponseMessage> CashCollectedAsync(HttpClient client, Guid shipmentId, long amountPaise, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/delivery/shipments/{shipmentId}/cash-collected")
        {
            Content = JsonContent.Create(new { amountPaise, mode = "CASH" }),
        };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Ct);
    }

    private static async Task<HttpResponseMessage> CreateSettlementAsync(HttpClient client, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/cash-settlements") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request, Ct);
    }

    private static async Task<HttpResponseMessage> PostWithVersionAsync(HttpClient client, string url, string etag, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return await client.SendAsync(request, Ct);
    }
}
