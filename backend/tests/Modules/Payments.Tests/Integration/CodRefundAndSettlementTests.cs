using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.Ordering.Public;
using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Handlers;
using FreshOrbit.Payments.Domain.Reconciliation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OrderingPaymentMethod = FreshOrbit.Ordering.Public.PaymentMethod;

namespace FreshOrbit.Payments.Tests.Integration;

/// <summary>Gap audit BE-5, BE-6, BE-7: refunds on collected pay-on-delivery orders and same-day settlements (D327-D329).</summary>
public sealed class CodRefundAndSettlementTests(PaymentsDatabase database) : PaymentsTestBase(database)
{
    private static readonly TimeZoneInfo Kolkata = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
    private static readonly string[] RefundPermissions = ["payments.view", "payments.manage", "refunds.request", "refunds.approve"];

    [Fact]
    public async Task BE5_goodwill_after_cash_was_collected_opens_refund_method_required_and_staff_refund_by_cash()
    {
        var locationId = Guid.CreateVersion7();
        var riderId = Guid.CreateVersion7();
        var rider = Factory.CreateStaffClient(["delivery.manage"], [locationId]);
        var requester = Factory.CreateStaffClient(RefundPermissions, [locationId]);
        var approver = Factory.CreateStaffClient(RefundPermissions, [locationId]);
        var (order, shipmentId) = await GivenCodShipmentAsync(riderId, locationId, 50000);
        (await CashCollectedAsync(rider, shipmentId, 50000)).EnsureSuccessStatusCode();
        var adjusted = new OrderAdjustedInput(order.OrderId, Guid.CreateVersion7(), 50000, 7500, Guid.CreateVersion7());

        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnOrderAdjustedAsync(adjusted, Ct));
        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnOrderAdjustedAsync(adjusted, Ct));

        var payment = (await LoadOrderPaymentsAsync(order.OrderId)).Single();
        payment.Refunds.Should().BeEmpty();
        var issues = await ListIssuesAsync(requester, payment.Id);
        var issue = issues.Should().ContainSingle().Which;
        issue.GetProperty("kind").GetString().Should().Be("REFUND_METHOD_REQUIRED");
        issue.GetProperty("subjectType").GetString().Should().Be("PAYMENT");
        issue.GetProperty("status").GetString().Should().Be("OPEN");
        issue.GetProperty("orderId").GetGuid().Should().Be(order.OrderId);

        // Staff choose CASH, a second person approves and marks it paid, then the issue is resolved.
        var created = await RequestRefundAsync(requester, order.OrderId, 7500, "CASH");
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var refundId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();
        var approved = await PostWithVersionAsync(approver, $"/api/v1/admin/refunds/{refundId}/approve", created.Headers.ETag!.Tag, new { });
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        var paid = await PostWithVersionAsync(approver, $"/api/v1/admin/refunds/{refundId}/mark-paid", approved.Headers.ETag!.Tag, new { reference = "Cash handed by rider" });
        paid.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(paid)).GetProperty("status").GetString().Should().Be("SUCCEEDED");

        var resolved = await PostWithVersionAsync(
            requester,
            $"/api/v1/admin/payment-issues/{issue.GetProperty("id").GetGuid()}/resolve",
            $"\"{issue.GetProperty("version").GetString()}\"",
            new { note = "Refunded in cash" });
        resolved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(resolved)).GetProperty("status").GetString().Should().Be("RESOLVED");
    }

    [Fact]
    public async Task BE5_goodwill_before_collection_lowers_the_amount_due_and_opens_no_issue()
    {
        var locationId = Guid.CreateVersion7();
        var (order, _) = await GivenCodShipmentAsync(Guid.CreateVersion7(), locationId, 50000);
        var adjusted = new OrderAdjustedInput(order.OrderId, Guid.CreateVersion7(), 42500, 0, Guid.CreateVersion7());

        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnOrderAdjustedAsync(adjusted, Ct));

        var payment = (await LoadOrderPaymentsAsync(order.OrderId)).Single();
        payment.AmountPaise.Should().Be(42500);
        await using var db = Database.CreateDbContext();
        (await db.Set<ReconciliationIssue>().CountAsync(i => i.OrderId == order.OrderId, Ct)).Should().Be(0);
    }

    [Fact]
    public async Task BE7_complaint_refund_original_on_a_cod_payment_opens_refund_method_required()
    {
        var locationId = Guid.CreateVersion7();
        var rider = Factory.CreateStaffClient(["delivery.manage"], [locationId]);
        var staff = Factory.CreateStaffClient(RefundPermissions, [locationId]);
        var (order, shipmentId) = await GivenCodShipmentAsync(Guid.CreateVersion7(), locationId, 30000);
        (await CashCollectedAsync(rider, shipmentId, 30000)).EnsureSuccessStatusCode();
        var decided = new ComplaintDecidedInput(order.OrderId, Guid.CreateVersion7(), "REFUND", 12000, "ORIGINAL", Guid.CreateVersion7());

        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnComplaintDecidedAsync(decided, Ct));
        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnComplaintDecidedAsync(decided, Ct));

        (await LoadOrderPaymentsAsync(order.OrderId)).Single().Refunds.Should().BeEmpty();
        var issue = (await ListIssuesAsync(staff, order.OrderId)).Should().ContainSingle().Which;
        issue.GetProperty("kind").GetString().Should().Be("REFUND_METHOD_REQUIRED");
        issue.GetProperty("subjectType").GetString().Should().Be("ORDER");
        issue.GetProperty("status").GetString().Should().Be("OPEN");
    }

    [Fact]
    public async Task BE6_cash_collected_after_the_days_settlement_goes_into_the_next_settlement()
    {
        var locationId = Guid.CreateVersion7();
        var riderId = Guid.CreateVersion7();
        Factory.Shipments.GetRiderAsync(riderId, Arg.Any<CancellationToken>()).Returns(new RiderFacts(riderId, locationId, "Asia/Kolkata", true));

        // Tokens are validated against the wall clock, so clients are made before the business clock moves.
        var rider = Factory.CreateStaffClient(["delivery.manage"], [locationId]);
        var recorder = Factory.CreateStaffClient(["cash.settle"], [locationId]);
        var start = Factory.Clock.UtcNow;
        var (first, firstShipment) = await GivenCodShipmentAsync(riderId, locationId, 10000);
        var (second, secondShipment) = await GivenCodShipmentAsync(riderId, locationId, 25000);

        Factory.Clock.Set(start.AddDays(-1));
        var yesterday = LocalDate(Factory.Clock.UtcNow);
        (await CashCollectedAsync(rider, firstShipment, 10000)).EnsureSuccessStatusCode();
        var early = await CreateSettlementAsync(recorder, new { riderId, date = yesterday, receivedPaise = 10000 });
        early.StatusCode.Should().Be(HttpStatusCode.Created, await early.Content.ReadAsStringAsync(Ct));
        (await ReadJsonAsync(early)).GetProperty("collectionsCount").GetInt32().Should().Be(1);

        // The rider collects again after the day's settlement was recorded.
        Factory.Clock.Advance(TimeSpan.FromHours(1));
        (await CashCollectedAsync(rider, secondShipment, 25000)).EnsureSuccessStatusCode();
        var again = await CreateSettlementAsync(recorder, new { riderId, date = yesterday, receivedPaise = 25000 });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(again)).Should().Be("SETTLEMENT_EXISTS");

        Factory.Clock.Set(start);
        var next = await CreateSettlementAsync(recorder, new { riderId, date = LocalDate(start), receivedPaise = 25000 });

        next.StatusCode.Should().Be(HttpStatusCode.Created, await next.Content.ReadAsStringAsync(Ct));
        var body = await ReadJsonAsync(next);
        body.GetProperty("expectedPaise").GetInt64().Should().Be(25000);
        body.GetProperty("differencePaise").GetInt64().Should().Be(0);
        body.GetProperty("collectionsCount").GetInt32().Should().Be(1);
        var nextId = body.GetProperty("id").GetGuid();
        var earlyId = (await ReadJsonAsync(early)).GetProperty("id").GetGuid();
        (await LoadOrderPaymentsAsync(first.OrderId)).Single().CashSettlementId.Should().Be(earlyId);
        (await LoadOrderPaymentsAsync(second.OrderId)).Single().CashSettlementId.Should().Be(nextId);
    }

    private static DateOnly LocalDate(DateTimeOffset utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, Kolkata).DateTime);

    private async Task<(OrderSummary Order, Guid ShipmentId)> GivenCodShipmentAsync(Guid riderId, Guid locationId, long amountPaise)
    {
        var order = GivenOrder(OrderStatus.OutForDelivery, OrderingPaymentMethod.Cod, amountPaise, locationId: locationId);
        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnOrderPlacedAsync(new OrderPlacedInput(order.OrderId, order.CustomerId, locationId, "COD", amountPaise), Ct));
        var shipmentId = Guid.CreateVersion7();
        Factory.Shipments.GetShipmentAsync(shipmentId, Arg.Any<CancellationToken>()).Returns(new ShipmentFacts(shipmentId, order.OrderId, riderId, locationId));
        return (order, shipmentId);
    }

    private static async Task<List<System.Text.Json.JsonElement>> ListIssuesAsync(HttpClient staff, Guid subjectId)
    {
        var response = await staff.GetAsync("/api/v1/admin/payment-issues?kind=REFUND_METHOD_REQUIRED&pageSize=100", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return [.. (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().Where(i => i.GetProperty("subjectId").GetGuid() == subjectId)];
    }

    private static async Task<HttpResponseMessage> CashCollectedAsync(HttpClient client, Guid shipmentId, long amountPaise)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/delivery/shipments/{shipmentId}/cash-collected")
        {
            Content = JsonContent.Create(new { amountPaise, mode = "CASH" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request, Ct);
    }

    private static async Task<HttpResponseMessage> RequestRefundAsync(HttpClient staff, Guid orderId, long amountPaise, string method)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/refunds")
        {
            Content = JsonContent.Create(new { amountPaise, reasonCode = "GOODWILL", method }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await staff.SendAsync(request, Ct);
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
