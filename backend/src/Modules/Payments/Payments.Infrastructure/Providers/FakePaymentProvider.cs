using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreshOrbit.Payments.Application.Payments;
using FreshOrbit.Payments.Application.Providers;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.SharedKernel.Time;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Payments.Infrastructure.Providers;

/// <summary>
/// In-memory state of the Fake gateway (one per process): orders, payments and refunds it "knows", plus outcomes
/// scripted by tests or the Development simulate endpoint. Ids carry a per-process tag so a restart never
/// reuses one already stored: <c>fake_order_{tag}_N</c>, <c>fake_pay_{tag}_N</c>, <c>fake_rfnd_{tag}_N</c>.
/// </summary>
public sealed class FakeGateway
{
    private readonly ConcurrentDictionary<string, FakeOrder> _orders = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, FakeRefund> _refundsByKey = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private readonly string _tag = Guid.NewGuid().ToString("N")[..8];
    private long _sequence;
    private int _transientRefundFailures;
    private int _refundCalls;
    private int _eventSequence;

    /// <summary>Outcome of the next provider refunds (default PROCESSED).</summary>
    public ProviderRefundStatus RefundOutcome { get; set; } = ProviderRefundStatus.Processed;

    /// <summary>When true every call throws as if the provider were down.</summary>
    public bool Unavailable { get; set; }

    /// <summary>Number of CreateRefund calls received (including failed ones).</summary>
    public int RefundCalls => _refundCalls;

    public int RefundsCreated => _refundsByKey.Count;

    /// <summary>The next <paramref name="count"/> refund calls fail with a transport error.</summary>
    public void FailNextRefundCalls(int count) => Interlocked.Exchange(ref _transientRefundFailures, count);

    public string NextId(string prefix) => $"{prefix}_{_tag}_{Interlocked.Increment(ref _sequence).ToString(CultureInfo.InvariantCulture)}";

    public string NextEventId() => $"evt_fake_{Interlocked.Increment(ref _eventSequence).ToString(CultureInfo.InvariantCulture)}_{Guid.NewGuid():N}";

    public FakeOrder CreateOrder(long amountPaise, string receipt)
    {
        var order = new FakeOrder(NextId("fake_order"), amountPaise, receipt);
        _orders[order.ProviderOrderId] = order;
        return order;
    }

    public FakeOrder? FindOrder(string providerOrderId) => _orders.TryGetValue(providerOrderId, out var order) ? order : null;

    /// <summary>Marks an order captured at the provider (what reconciliation will see); returns the provider payment id.</summary>
    public string Capture(string providerOrderId, long amountPaise)
    {
        var order = _orders.GetOrAdd(providerOrderId, id => new FakeOrder(id, amountPaise, string.Empty));
        lock (_lock)
        {
            var paymentId = NextId("fake_pay");
            order.Attempts.Add(new ProviderPaymentAttempt(paymentId, ProviderAttemptStatus.Captured, amountPaise, PaymentProviders.Inr));
            return paymentId;
        }
    }

    public ProviderRefundResult CreateRefund(ProviderRefundRequest request)
    {
        Interlocked.Increment(ref _refundCalls);
        ThrowIfUnavailable();
        if (Interlocked.Decrement(ref _transientRefundFailures) >= 0)
        {
            throw new PaymentProviderUnavailableException("The Fake gateway is temporarily unavailable.");
        }

        var refund = _refundsByKey.GetOrAdd(request.IdempotencyKey, _ => new FakeRefund(NextId("fake_rfnd"), request.ProviderPaymentId, request.AmountPaise, RefundOutcome));
        return new ProviderRefundResult(refund.ProviderRefundId, refund.Status, refund.Status == ProviderRefundStatus.Failed ? "FAKE_REFUND_FAILED" : null);
    }

    public ProviderRefundState GetRefund(string providerRefundId)
    {
        ThrowIfUnavailable();
        var refund = _refundsByKey.Values.FirstOrDefault(r => r.ProviderRefundId == providerRefundId);
        return refund is null
            ? new ProviderRefundState(providerRefundId, ProviderRefundStatus.Failed, "FAKE_REFUND_UNKNOWN")
            : new ProviderRefundState(providerRefundId, refund.Status, null);
    }

    /// <summary>Settles every pending refund with <paramref name="status"/> (as a later provider answer would).</summary>
    public void SettlePendingRefunds(ProviderRefundStatus status)
    {
        foreach (var refund in _refundsByKey.Values.Where(r => r.Status == ProviderRefundStatus.Pending))
        {
            refund.Status = status;
        }
    }

    public void ThrowIfUnavailable()
    {
        if (Unavailable)
        {
            throw new PaymentProviderUnavailableException("The Fake gateway is down.");
        }
    }

    public sealed class FakeOrder(string providerOrderId, long amountPaise, string receipt)
    {
        public string ProviderOrderId { get; } = providerOrderId;

        public long AmountPaise { get; } = amountPaise;

        public string Receipt { get; } = receipt;

        public List<ProviderPaymentAttempt> Attempts { get; } = [];
    }

    private sealed class FakeRefund(string providerRefundId, string providerPaymentId, long amountPaise, ProviderRefundStatus status)
    {
        public string ProviderRefundId { get; } = providerRefundId;

        public string ProviderPaymentId { get; } = providerPaymentId;

        public long AmountPaise { get; } = amountPaise;

        public ProviderRefundStatus Status { get; set; } = status;
    }
}

/// <summary>
/// The Fake gateway adapter (PAY-R2): all capabilities; webhooks signed with HMAC-SHA256 (hex) of the raw body in
/// <c>X-Fake-Signature</c>; event id = body <c>id</c>. Enabled only by configuration and refused in Production.
/// </summary>
public sealed class FakePaymentProvider(FakeGateway gateway, IOptionsMonitor<PaymentProvidersOptions> options, IClock clock) : IPaymentProvider, IHasPublicKeyId
{
    public const string SignatureHeader = "X-Fake-Signature";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public string Key => PaymentProviders.Fake;

    public ProviderCapabilities Capabilities => ProviderCapabilities.OnlineCheckout | ProviderCapabilities.Webhooks | ProviderCapabilities.ProviderRefunds;

    public string? PublicKeyId => Settings.KeyId ?? "fake_key";

    private ProviderSettings Settings => options.CurrentValue.For(PaymentProviders.Fake) ?? new ProviderSettings();

    public Task<ProviderOrder> CreateOrderAsync(CreateProviderOrder request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        gateway.ThrowIfUnavailable();
        var order = gateway.CreateOrder(request.AmountPaise, request.PaymentId.ToString());
        var parameters = new Dictionary<string, string> { ["keyId"] = PublicKeyId!, ["receipt"] = order.Receipt };
        return Task.FromResult(new ProviderOrder(order.ProviderOrderId, parameters, request.ExpiresAt ?? clock.UtcNow.AddMinutes(15)));
    }

    public bool VerifySignature(IReadOnlyDictionary<string, string> headers, ReadOnlySpan<byte> rawBody)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (!headers.TryGetValue(SignatureHeader, out var signature) || string.IsNullOrWhiteSpace(Settings.WebhookSecret))
        {
            return false;
        }

        return VerifyHmac(Settings.WebhookSecret, signature, rawBody);
    }

    public ProviderWebhook ParseWebhook(IReadOnlyDictionary<string, string> headers, ReadOnlySpan<byte> rawBody)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(rawBody);
        }
        catch (JsonException exception)
        {
            throw new FormatException("The body is not JSON.", exception);
        }

        var id = root?["id"]?.GetValue<string>() ?? throw new FormatException("The body has no 'id'.");
        var type = root["type"]?.GetValue<string>() ?? throw new FormatException("The body has no 'type'.");
        var data = root["data"];
        return new ProviderWebhook(
            id,
            type switch
            {
                "payment.authorized" => ProviderWebhookKind.PaymentAuthorized,
                "payment.captured" => ProviderWebhookKind.PaymentCaptured,
                "payment.failed" => ProviderWebhookKind.PaymentFailed,
                "refund.processed" => ProviderWebhookKind.RefundProcessed,
                "refund.failed" => ProviderWebhookKind.RefundFailed,
                _ => ProviderWebhookKind.Other,
            },
            type,
            Text(data, "providerOrderId"),
            Text(data, "providerPaymentId"),
            Text(data, "providerRefundId"),
            data?["amountPaise"]?.GetValue<long>(),
            Text(data, "currency"),
            Text(data, "failureCode"),
            Text(data, "failureMessage"),
            Text(data, "method"));
    }

    public Task<ProviderPaymentState> GetPaymentAsync(string providerOrderId, CancellationToken cancellationToken)
    {
        gateway.ThrowIfUnavailable();
        var order = gateway.FindOrder(providerOrderId);
        return Task.FromResult(new ProviderPaymentState(providerOrderId, order is null ? [] : [.. order.Attempts]));
    }

    public Task<ProviderRefundResult> CreateRefundAsync(ProviderRefundRequest request, CancellationToken cancellationToken)
        => Task.FromResult(gateway.CreateRefund(request));

    public Task<ProviderRefundState> GetRefundAsync(string providerRefundId, CancellationToken cancellationToken)
        => Task.FromResult(gateway.GetRefund(providerRefundId));

    /// <summary>HMAC-SHA256 hex of <paramref name="body"/> with <paramref name="secret"/>.</summary>
    public static string Sign(string secret, ReadOnlySpan<byte> body)
        => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

    /// <summary>Constant-time comparison of the expected and given hex signatures; malformed or wrong length → false.</summary>
    public static bool VerifyHmac(string secret, string signatureHex, ReadOnlySpan<byte> body)
    {
        ArgumentNullException.ThrowIfNull(signatureHex);
        Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body, expected);
        byte[] given;
        try
        {
            given = Convert.FromHexString(signatureHex.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        return given.Length == expected.Length && CryptographicOperations.FixedTimeEquals(expected, given);
    }

    /// <summary>Builds a webhook body in the Fake format.</summary>
    public static byte[] BuildBody(string eventId, string type, DateTimeOffset createdAt, object data)
    {
        var body = new
        {
            id = eventId,
            type,
            createdAt,
            data,
        };
        return JsonSerializer.SerializeToUtf8Bytes(body, WebJson);
    }

    private static string? Text(JsonNode? node, string name) => node?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
