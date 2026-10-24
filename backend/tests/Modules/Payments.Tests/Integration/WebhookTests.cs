using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Ordering.Public;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.Payments.Domain.Webhooks;
using FreshOrbit.Payments.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using OrderingPaymentMethod = FreshOrbit.Ordering.Public.PaymentMethod;
using PaymentStatus = FreshOrbit.Payments.Domain.Payments.PaymentStatus;

namespace FreshOrbit.Payments.Tests.Integration;

public sealed class WebhookTests(PaymentsDatabase database) : PaymentsTestBase(database)
{
    [Fact]
    public async Task I1_duplicate_webhook_has_one_effect_sequentially_and_concurrently()
    {
        var (providerOrderId, providerPaymentId, paymentId) = await GivenInitiatedAttemptAsync(66950);
        var eventId = NewEventId();

        var first = await SendWebhookAsync(eventId, "payment.captured", providerOrderId, providerPaymentId, 66950);
        var second = await SendWebhookAsync(eventId, "payment.captured", providerOrderId, providerPaymentId, 66950);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(first)).GetProperty("duplicate").GetBoolean().Should().BeFalse();
        (await ReadJsonAsync(second)).GetProperty("duplicate").GetBoolean().Should().BeTrue();

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SendWebhookAsync(eventId, "payment.captured", providerOrderId, providerPaymentId, 66950)));
        concurrent.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        foreach (var response in concurrent)
        {
            (await ReadJsonAsync(response)).GetProperty("duplicate").GetBoolean().Should().BeTrue();
        }

        (await LoadPaymentAsync(paymentId)).Status.Should().Be(PaymentStatus.Captured);
        (await CountEventsAsync(eventId)).Should().Be(1);
        (await CountOutboxAsync("PaymentCaptured", paymentId)).Should().Be(1);
    }

    [Fact]
    public async Task I1_first_delivery_racing_with_itself_is_applied_exactly_once()
    {
        var (providerOrderId, providerPaymentId, paymentId) = await GivenInitiatedAttemptAsync(47950);
        var eventId = NewEventId();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SendWebhookAsync(eventId, "payment.captured", providerOrderId, providerPaymentId, 47950)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        var duplicates = new List<bool>();
        foreach (var response in responses)
        {
            duplicates.Add((await ReadJsonAsync(response)).GetProperty("duplicate").GetBoolean());
        }

        duplicates.Count(d => !d).Should().Be(1);
        (await CountEventsAsync(eventId)).Should().Be(1);
        (await CountOutboxAsync("PaymentCaptured", paymentId)).Should().Be(1);
    }

    [Fact]
    public async Task Same_state_with_another_event_id_is_processed_without_a_second_event()
    {
        var (providerOrderId, providerPaymentId, paymentId) = await GivenInitiatedAttemptAsync(5000);
        await SendWebhookAsync(NewEventId(), "payment.captured", providerOrderId, providerPaymentId, 5000);

        var again = await SendWebhookAsync(NewEventId(), "payment.captured", providerOrderId, providerPaymentId, 5000);

        (await ReadJsonAsync(again)).GetProperty("duplicate").GetBoolean().Should().BeFalse();
        (await CountOutboxAsync("PaymentCaptured", paymentId)).Should().Be(1);
    }

    [Fact]
    public async Task I2_invalid_signature_is_stored_rejected_and_refused_and_a_later_valid_event_is_processed()
    {
        var (providerOrderId, providerPaymentId, paymentId) = await GivenInitiatedAttemptAsync(66950);
        var eventId = NewEventId();
        var body = FakePaymentProvider.BuildBody(eventId, "payment.captured", Factory.Clock.UtcNow, new { providerOrderId, providerPaymentId, amountPaise = 66950L, currency = "INR" });

        var rejected = await PostWebhookAsync(body, FakePaymentProvider.Sign("wrong-secret", body));

        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeAsync(rejected)).Should().Be("WEBHOOK_SIGNATURE_INVALID");
        (await LoadPaymentAsync(paymentId)).Status.Should().Be(PaymentStatus.Pending);
        await using (var db = Database.CreateDbContext())
        {
            var row = await db.Set<WebhookEvent>().SingleAsync(e => e.ProviderEventId == eventId, Ct);
            row.SignatureValid.Should().BeFalse();
            row.Status.Should().Be(WebhookEventStatus.Rejected);
        }

        var valid = await PostWebhookAsync(body, FakePaymentProvider.Sign(PaymentsApiFactory.FakeSecret, body));

        valid.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(valid)).GetProperty("duplicate").GetBoolean().Should().BeFalse();
        (await LoadPaymentAsync(paymentId)).Status.Should().Be(PaymentStatus.Captured);
    }

    [Fact]
    public async Task SEC6_body_over_64_kb_is_refused_with_413_and_nothing_is_stored()
    {
        var (providerOrderId, providerPaymentId, paymentId) = await GivenInitiatedAttemptAsync(2500);
        var eventId = NewEventId();
        var body = FakePaymentProvider.BuildBody(eventId, "payment.captured", Factory.Clock.UtcNow, new { providerOrderId, providerPaymentId, amountPaise = 2500L, currency = "INR", note = new string('x', 70_000) });
        body.Length.Should().BeGreaterThan(WebhookEvent.MaxBodyBytes);

        var withLength = await PostWebhookAsync(body, FakePaymentProvider.Sign(PaymentsApiFactory.FakeSecret, body));

        using var client = Factory.CreateClient();
        using var chunked = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/fake") { Content = new StreamContent(new UnseekableStream(body)) };
        chunked.Headers.Add(FakePaymentProvider.SignatureHeader, FakePaymentProvider.Sign(PaymentsApiFactory.FakeSecret, body));
        var withoutLength = await client.SendAsync(chunked, Ct);

        foreach (var response in new[] { withLength, withoutLength })
        {
            response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
            (await CodeAsync(response)).Should().Be("PAYLOAD_TOO_LARGE");
        }

        await using var db = Database.CreateDbContext();
        (await db.Set<WebhookEvent>().CountAsync(e => e.ProviderEventId == eventId, Ct)).Should().Be(0);
        (await LoadPaymentAsync(paymentId)).Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task SEC6_rejected_event_keeps_only_the_first_4_kb_and_the_sha256_of_the_whole_body()
    {
        var eventId = NewEventId();
        var big = FakePaymentProvider.BuildBody(eventId, "payment.captured", Factory.Clock.UtcNow, new { providerOrderId = "fake_order_x", amountPaise = 100L, currency = "INR", note = new string('y', 10_000) });
        var smallEventId = NewEventId();
        var small = FakePaymentProvider.BuildBody(smallEventId, "payment.captured", Factory.Clock.UtcNow, new { providerOrderId = "fake_order_y", amountPaise = 100L, currency = "INR" });

        var bigResponse = await PostWebhookAsync(big, FakePaymentProvider.Sign("wrong-secret", big));
        var smallResponse = await PostWebhookAsync(small, FakePaymentProvider.Sign("wrong-secret", small));

        bigResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        smallResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var db = Database.CreateDbContext();
        var bigRow = await db.Set<WebhookEvent>().SingleAsync(e => e.ProviderEventId == eventId, Ct);
        bigRow.Status.Should().Be(WebhookEventStatus.Rejected);
        bigRow.PayloadTruncated.Should().BeTrue();
        bigRow.PayloadSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(big)));
        var kept = JsonSerializer.Deserialize<string>(bigRow.Payload)!;
        kept.Should().Be(Encoding.UTF8.GetString(big, 0, WebhookEvent.MaxRejectedPayloadBytes));

        var smallRow = await db.Set<WebhookEvent>().SingleAsync(e => e.ProviderEventId == smallEventId, Ct);
        smallRow.PayloadTruncated.Should().BeFalse();
        smallRow.PayloadSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(small)));
        JsonSerializer.Deserialize<string>(smallRow.Payload).Should().Be(Encoding.UTF8.GetString(small));

        // Not JSON and with a NUL byte (jsonb refuses \u0000): still stored, found by its hash.
        var junk = Encoding.UTF8.GetBytes("junk\0" + Guid.NewGuid().ToString("N"));
        (await PostWebhookAsync(junk, "00")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var junkHash = Convert.ToHexStringLower(SHA256.HashData(junk));
        (await db.Set<WebhookEvent>().SingleAsync(e => e.PayloadSha256 == junkHash, Ct)).ProviderEventId.Should().Be("UNREADABLE");
    }

    [Fact]
    public async Task I8_amount_mismatch_keeps_the_payment_pending_and_raises_an_alert()
    {
        var (providerOrderId, providerPaymentId, paymentId) = await GivenInitiatedAttemptAsync(66950);
        var eventId = NewEventId();

        var response = await SendWebhookAsync(eventId, "payment.captured", providerOrderId, providerPaymentId, 70000);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoadPaymentAsync(paymentId)).Status.Should().Be(PaymentStatus.Pending);
        await using var db = Database.CreateDbContext();
        var row = await db.Set<WebhookEvent>().SingleAsync(e => e.ProviderEventId == eventId, Ct);
        row.Status.Should().Be(WebhookEventStatus.Error);
        row.Error.Should().Be("AMOUNT_MISMATCH");
        var issue = await db.Set<ReconciliationIssue>().SingleAsync(i => i.SubjectId == paymentId, Ct);
        issue.Kind.Should().Be(IssueKind.AmountMismatch);
        issue.Status.Should().Be(IssueStatus.Open);
        (await CountOutboxAsync("PaymentAlertRaised", issue.Id)).Should().Be(1);
        (await CountOutboxAsync("PaymentCaptured", paymentId)).Should().Be(0);
    }

    [Fact]
    public async Task Failed_webhook_fails_the_attempt_and_reports_other_active_attempts()
    {
        var (providerOrderId, _, paymentId) = await GivenInitiatedAttemptAsync(1000);

        var response = await SendWebhookAsync(NewEventId(), "payment.failed", providerOrderId, "fake_pay_failed_" + Guid.NewGuid().ToString("N"), 1000);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoadPaymentAsync(paymentId)).Status.Should().Be(PaymentStatus.Failed);
        (await CountOutboxAsync("PaymentFailed", paymentId)).Should().Be(1);
    }

    [Fact]
    public async Task Unknown_provider_is_404_and_unknown_payment_is_stored_as_error()
    {
        using var client = Factory.CreateClient();
        var unknown = await client.PostAsync("/api/v1/payments/webhooks/nopay", new ByteArrayContent(Encoding.UTF8.GetBytes("{}")), Ct);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeAsync(unknown)).Should().Be("WEBHOOK_PROVIDER_UNKNOWN");

        var eventId = NewEventId();
        var response = await SendWebhookAsync(eventId, "payment.captured", "fake_order_unknown_" + Guid.NewGuid().ToString("N"), null, 100);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = Database.CreateDbContext();
        (await db.Set<WebhookEvent>().SingleAsync(e => e.ProviderEventId == eventId, Ct)).Error.Should().Be("UNKNOWN_PAYMENT");
    }

    [Fact]
    public async Task I14_initiate_is_refused_for_cod_and_reuses_a_valid_attempt()
    {
        var customerId = Guid.CreateVersion7();
        var customer = Factory.CreateCustomerClient(customerId: customerId);
        var cod = GivenOrder(OrderStatus.Confirmed, OrderingPaymentMethod.Cod, 234900, customerId);
        using (var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/orders/{cod.OrderId}/payments"))
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var refused = await customer.SendAsync(request, Ct);
            refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CodeAsync(refused)).Should().Be("PAYMENT_NOT_REQUIRED");
        }

        var online = GivenOrder(OrderStatus.PendingPayment, OrderingPaymentMethod.Online, 66950, customerId);
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/orders/{online.OrderId}/payments");
        firstRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var first = await customer.SendAsync(firstRequest, Ct);
        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/orders/{online.OrderId}/payments");
        secondRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var second = await customer.SendAsync(secondRequest, Ct);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await ReadJsonAsync(first);
        var secondBody = await ReadJsonAsync(second);
        secondBody.GetProperty("providerOrderId").GetString().Should().Be(firstBody.GetProperty("providerOrderId").GetString());
        firstBody.GetProperty("keyId").GetString().Should().Be("fake_key_test");
        firstBody.GetProperty("amountPaise").GetInt64().Should().Be(66950);
        (await LoadOrderPaymentsAsync(online.OrderId)).Should().ContainSingle();

        var stranger = Factory.CreateCustomerClient();
        (await stranger.GetAsync($"/api/v1/orders/{online.OrderId}/payments", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var own = await customer.GetAsync($"/api/v1/orders/{online.OrderId}/payments", Ct);
        own.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(own)).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Development_simulate_endpoint_captures_through_the_signed_webhook_path()
    {
        var (providerOrderId, _, paymentId) = await GivenInitiatedAttemptAsync(12345, captureAtProvider: false);
        using var client = Factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/v1/dev/payments/fake/{providerOrderId}/simulate",
            System.Net.Http.Json.JsonContent.Create(new { outcome = "CAPTURED" }),
            Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoadPaymentAsync(paymentId)).Status.Should().Be(PaymentStatus.Captured);
    }

    private async Task<(string ProviderOrderId, string ProviderPaymentId, Guid PaymentId)> GivenInitiatedAttemptAsync(long amountPaise, bool captureAtProvider = true)
    {
        var customerId = Guid.CreateVersion7();
        var order = GivenOrder(OrderStatus.PendingPayment, OrderingPaymentMethod.Online, amountPaise, customerId);
        var initiated = await InitiateAsync(Factory.CreateCustomerClient(customerId: customerId), order.OrderId);
        var providerOrderId = initiated.GetProperty("providerOrderId").GetString()!;
        var providerPaymentId = captureAtProvider ? Factory.Gateway.Capture(providerOrderId, amountPaise) : string.Empty;
        return (providerOrderId, providerPaymentId, initiated.GetProperty("paymentId").GetGuid());
    }

    private async Task<int> CountEventsAsync(string eventId)
    {
        await using var db = Database.CreateDbContext();
        return await db.Set<WebhookEvent>().CountAsync(e => e.ProviderEventId == eventId && e.SignatureValid, Ct);
    }
}

/// <summary>Hides the length so the client sends the body chunked, without Content-Length.</summary>
internal sealed class UnseekableStream(byte[] bytes) : Stream
{
    private readonly MemoryStream inner = new(bytes, writable: false);

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
