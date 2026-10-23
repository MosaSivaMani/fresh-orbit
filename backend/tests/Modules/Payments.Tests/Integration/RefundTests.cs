using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.Payments.Application.Handlers;
using FreshOrbit.Payments.Public.Events;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.SharedKernel.Events;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Payments.Tests.Integration;

public sealed class RefundTests(PaymentsDatabase database) : PaymentsTestBase(database)
{
    private static readonly string[] RefundPermissions = ["payments.view", "refunds.request", "refunds.approve"];

    [Fact]
    public async Task I3_concurrent_refunds_exceeding_the_paid_amount_let_exactly_one_succeed()
    {
        var locationId = Guid.CreateVersion7();
        var (order, paymentId) = await GivenCapturedOnlineOrderAsync(66950, locationId);
        var staff = Factory.CreateStaffClient(RefundPermissions, [locationId]);

        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => RequestRefundAsync(staff, order.OrderId, 40000, "ORIGINAL")));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        var refused = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();
        refused.Should().HaveCount(2).And.OnlyContain(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
        foreach (var response in refused)
        {
            (await CodeAsync(response)).Should().Be("REFUND_EXCEEDS_PAYMENT");
        }

        var payment = await LoadPaymentAsync(paymentId);
        payment.Refunds.Should().ContainSingle();
        payment.RefundPendingPaise.Should().Be(40000);
        payment.RefundablePaise.Should().Be(26950);
    }

    [Fact]
    public async Task Staff_refund_needs_a_second_person_and_a_version()
    {
        var locationId = Guid.CreateVersion7();
        var requesterId = Guid.CreateVersion7();
        var (order, _) = await GivenCapturedOnlineOrderAsync(10000, locationId);
        var requester = Factory.CreateStaffClient(RefundPermissions, [locationId], userId: requesterId);
        var approver = Factory.CreateStaffClient(RefundPermissions, [locationId]);

        var created = await RequestRefundAsync(requester, order.OrderId, 2500, "ORIGINAL");
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var refundId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();
        var etag = created.Headers.ETag!.Tag;

        (await PostAsync(approver, $"/api/v1/admin/refunds/{refundId}/approve", null)).StatusCode.Should().Be((HttpStatusCode)428);
        var self = await PostAsync(requester, $"/api/v1/admin/refunds/{refundId}/approve", etag);
        self.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeAsync(self)).Should().Be("REFUND_SELF_APPROVAL");
        (await PostAsync(approver, $"/api/v1/admin/refunds/{refundId}/approve", "\"1\"")).StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var approved = await PostAsync(approver, $"/api/v1/admin/refunds/{refundId}/approve", etag);
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(approved)).GetProperty("status").GetString().Should().Be("APPROVED");

        var stale = await PostAsync(approver, $"/api/v1/admin/refunds/{refundId}/reject", etag, new { reason = "Too late" });
        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task Refund_endpoints_need_their_permission_and_location()
    {
        var locationId = Guid.CreateVersion7();
        var (order, _) = await GivenCapturedOnlineOrderAsync(10000, locationId);

        var viewer = Factory.CreateStaffClient(["payments.view"], [locationId]);
        (await RequestRefundAsync(viewer, order.OrderId, 100, "ORIGINAL")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var otherLocation = Factory.CreateStaffClient(RefundPermissions, [Guid.CreateVersion7()]);
        var denied = await RequestRefundAsync(otherLocation, order.OrderId, 100, "ORIGINAL");
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeAsync(denied)).Should().Be("LOCATION_ACCESS_DENIED");

        (await Factory.CreateCustomerClient().GetAsync("/api/v1/admin/refunds", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task I5_cancel_after_capture_refunds_in_full_once_and_the_provider_refund_job_is_idempotent()
    {
        var (order, paymentId) = await GivenCapturedOnlineOrderAsync(66950);

        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnOrderCancelledAsync(order.OrderId, Ct));
        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnOrderCancelledAsync(order.OrderId, Ct));

        var payment = await LoadPaymentAsync(paymentId);
        var refund = payment.Refunds.Should().ContainSingle().Which;
        refund.Status.Should().Be(RefundStatus.Approved);
        refund.AmountPaise.Should().Be(66950);
        refund.ReasonCode.Should().Be(RefundReason.Cancellation);
        refund.RequestedBy.Should().BeNull();
        (await CountOutboxAsync("RefundApproved", refund.Id)).Should().Be(1);

        var approved = new RefundApproved(refund.Id, order.OrderId, "ORIGINAL");
        await RunRefundHandlerAsync(approved);
        await RunRefundHandlerAsync(approved);

        payment = await LoadPaymentAsync(paymentId);
        refund = payment.Refunds.Single();
        refund.Status.Should().Be(RefundStatus.Succeeded);
        refund.ProviderRefundId.Should().StartWith("fake_rfnd_");
        payment.RefundedPaise.Should().Be(66950);
        Factory.Gateway.RefundCalls.Should().Be(1);
        (await CountOutboxAsync("RefundSucceeded", refund.Id)).Should().Be(1);

        var webhook = await SendWebhookAsync(NewEventId(), "refund.processed", payment.ProviderOrderId!, payment.ProviderPaymentId, 66950, providerRefundId: refund.ProviderRefundId);
        webhook.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountOutboxAsync("RefundSucceeded", refund.Id)).Should().Be(1);
    }

    [Fact]
    public async Task I11_transient_provider_errors_retry_with_the_same_key_and_create_one_provider_refund()
    {
        var (order, paymentId) = await GivenCapturedOnlineOrderAsync(5000);
        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnOrderCancelledAsync(order.OrderId, Ct));
        var refundId = (await LoadPaymentAsync(paymentId)).Refunds.Single().Id;
        Factory.Gateway.FailNextRefundCalls(2);

        var approved = new RefundApproved(refundId, order.OrderId, "ORIGINAL");
        var failing = () => RunRefundHandlerAsync(approved);
        await failing.Should().ThrowAsync<Exception>();
        await failing.Should().ThrowAsync<Exception>();
        (await LoadPaymentAsync(paymentId)).Refunds.Single().Status.Should().Be(RefundStatus.Processing);

        await RunRefundHandlerAsync(approved);

        var refund = (await LoadPaymentAsync(paymentId)).Refunds.Single();
        refund.Status.Should().Be(RefundStatus.Succeeded);
        refund.AttemptCount.Should().Be(3);
        Factory.Gateway.RefundCalls.Should().Be(3);
        Factory.Gateway.RefundsCreated.Should().Be(1);
    }

    [Fact]
    public async Task Packing_shortfall_creates_an_approved_weight_shortfall_refund_once()
    {
        var (order, paymentId) = await GivenCapturedOnlineOrderAsync(66950);
        var input = new OrderPackedInput(order.OrderId, "ONLINE", 60255, 6695, new Dictionary<string, long>(StringComparer.Ordinal) { ["WEIGHT_SHORTFALL"] = 6695 });

        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnOrderPackedAsync(input, Ct));
        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnOrderPackedAsync(input, Ct));

        var refund = (await LoadPaymentAsync(paymentId)).Refunds.Should().ContainSingle().Which;
        refund.ReasonCode.Should().Be(RefundReason.WeightShortfall);
        refund.AmountPaise.Should().Be(6695);
        refund.Status.Should().Be(RefundStatus.Approved);
    }

    [Fact]
    public async Task Customer_sees_own_refunds_without_staff_fields()
    {
        var (order, _) = await GivenCapturedOnlineOrderAsync(5000);
        await InScopeAsync(sp => sp.GetRequiredService<OrderPaymentHandlers>().OnOrderCancelledAsync(order.OrderId, Ct));

        var response = await Factory.CreateCustomerClient(customerId: order.CustomerId).GetAsync($"/api/v1/orders/{order.OrderId}/refunds", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await ReadJsonAsync(response)).EnumerateArray().Should().ContainSingle().Which;
        item.GetProperty("amountPaise").GetInt64().Should().Be(5000);
        item.TryGetProperty("requestedBy", out _).Should().BeFalse();
    }

    private async Task RunRefundHandlerAsync(RefundApproved approved)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IEventHandler<RefundApproved>>().OfType<FreshOrbit.Payments.Infrastructure.Jobs.RefundApprovedHandler>().Single();
        await handler.HandleAsync(approved, Ct);
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

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, string? ifMatch, object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await client.SendAsync(request, Ct);
    }
}
