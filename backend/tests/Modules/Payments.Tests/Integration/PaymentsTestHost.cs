using System.Net.Http.Json;
using System.Text.Json;
using FreshOrbit.Ordering.Public;
using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.Payments.Infrastructure.Providers;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using OrderingPaymentMethod = FreshOrbit.Ordering.Public.PaymentMethod;

[assembly: AssemblyFixture(typeof(FreshOrbit.Payments.Tests.Integration.PaymentsDatabase))]

namespace FreshOrbit.Payments.Tests.Integration;

/// <summary>One Postgres for the assembly; tests use their own ids so they never see each other's rows.</summary>
public sealed class PaymentsDatabase : PostgresFixture;

/// <summary>The real Api with the Fake gateway enabled and Ordering / Delivery facts substituted.</summary>
public sealed class PaymentsApiFactory(PaymentsDatabase database) : ApiFactory(database)
{
    public const string FakeSecret = "test-fake-webhook-secret-0123456789";

    public IOrderingApi Ordering { get; } = Substitute.For<IOrderingApi>();

    public IShipmentFacts Shipments { get; } = Substitute.For<IShipmentFacts>();

    public FakeGateway Gateway => Services.GetRequiredService<FakeGateway>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Payments:OnlineProvider", PaymentProviders.Fake);
        builder.UseSetting("Payments:Providers:FAKE:Enabled", "true");
        builder.UseSetting("Payments:Providers:FAKE:KeyId", "fake_key_test");
        builder.UseSetting("Payments:Providers:FAKE:WebhookSecret", FakeSecret);
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton(Ordering));
        services.Replace(ServiceDescriptor.Singleton(Shipments));
    }
}

/// <summary>Starts a fresh Api per test and offers the scenario helpers the payment tests share.</summary>
public abstract class PaymentsTestBase(PaymentsDatabase database) : IAsyncLifetime
{
    protected PaymentsApiFactory Factory { get; } = new(database);

    protected PaymentsDatabase Database => database;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).Clone();

    protected static async Task<string?> CodeAsync(HttpResponseMessage response) => (await ReadJsonAsync(response)).GetProperty("code").GetString();

    /// <summary>Registers an order with Ordering's substitute.</summary>
    protected OrderSummary GivenOrder(OrderStatus status, OrderingPaymentMethod method, long amountDuePaise, Guid? customerId = null, Guid? locationId = null)
    {
        var order = new OrderSummary(Guid.CreateVersion7(), "FO-" + Random.Shared.Next(100000, 999999), customerId ?? Guid.CreateVersion7(), status, method, amountDuePaise, null, amountDuePaise, locationId ?? Guid.CreateVersion7());
        SetOrder(order);
        return order;
    }

    protected void SetOrder(OrderSummary order)
    {
        ArgumentNullException.ThrowIfNull(order);
        Factory.Ordering.GetOrderSummaryAsync(order.OrderId, Arg.Any<CancellationToken>()).Returns(order);
    }

    /// <summary>Runs <paramref name="work"/> in a DI scope of the Api (as a handler / job would) and saves its tracked changes.</summary>
    protected async Task InScopeAsync(Func<IServiceProvider, Task> work, bool save = true)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var scope = Factory.Services.CreateAsyncScope();
        await work(scope.ServiceProvider);
        if (save)
        {
            await scope.ServiceProvider.GetRequiredService<FreshOrbitDbContext>().SaveChangesAsync(Ct);
        }
    }

    protected async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var scope = Factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    /// <summary>An ONLINE order with a captured payment (initiated by the customer, captured by a signed webhook).</summary>
    protected async Task<(OrderSummary Order, Guid PaymentId)> GivenCapturedOnlineOrderAsync(long amountPaise, Guid? locationId = null)
    {
        var customerId = Guid.CreateVersion7();
        var order = GivenOrder(OrderStatus.PendingPayment, OrderingPaymentMethod.Online, amountPaise, customerId, locationId);
        var customer = Factory.CreateCustomerClient(customerId: customerId);
        var initiated = await InitiateAsync(customer, order.OrderId);
        var providerOrderId = initiated.GetProperty("providerOrderId").GetString()!;
        var providerPaymentId = Factory.Gateway.Capture(providerOrderId, amountPaise);
        var response = await SendWebhookAsync(NewEventId(), "payment.captured", providerOrderId, providerPaymentId, amountPaise);
        response.EnsureSuccessStatusCode();
        SetOrder(order with { Status = OrderStatus.Confirmed });
        return (order, initiated.GetProperty("paymentId").GetGuid());
    }

    protected static async Task<JsonElement> InitiateAsync(HttpClient customer, Guid orderId, string? key = null)
    {
        ArgumentNullException.ThrowIfNull(customer);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/orders/{orderId}/payments");
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        var response = await customer.SendAsync(request, Ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Initiate failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        }

        return await ReadJsonAsync(response);
    }

    protected static string NewEventId() => "evt_" + Guid.NewGuid().ToString("N");

    protected Task<HttpResponseMessage> SendWebhookAsync(string eventId, string type, string providerOrderId, string? providerPaymentId, long amountPaise, string currency = "INR", string? signature = null, string? providerRefundId = null)
    {
        var body = FakePaymentProvider.BuildBody(eventId, type, Factory.Clock.UtcNow, new { providerOrderId, providerPaymentId, providerRefundId, amountPaise, currency, method = "UPI" });
        return PostWebhookAsync(body, signature ?? FakePaymentProvider.Sign(PaymentsApiFactory.FakeSecret, body));
    }

    protected async Task<HttpResponseMessage> PostWebhookAsync(byte[] body, string signature)
    {
        ArgumentNullException.ThrowIfNull(body);
        using var client = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/fake") { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add(FakePaymentProvider.SignatureHeader, signature);
        return await client.SendAsync(request, Ct);
    }

    protected async Task<Payment> LoadPaymentAsync(Guid paymentId)
    {
        await using var db = Database.CreateDbContext();
        return await db.Set<Payment>().AsNoTracking().Include(p => p.Refunds).SingleAsync(p => p.Id == paymentId, Ct);
    }

    protected async Task<List<Payment>> LoadOrderPaymentsAsync(Guid orderId)
    {
        await using var db = Database.CreateDbContext();
        return await db.Set<Payment>().AsNoTracking().Include(p => p.Refunds).Where(p => p.OrderId == orderId).ToListAsync(Ct);
    }

    /// <summary>Outbox rows of an event type whose payload mentions <paramref name="id"/>.</summary>
    protected async Task<int> CountOutboxAsync(string eventName, Guid id)
    {
        await using var db = Database.CreateDbContext();
        var type = $"%.{eventName}, FreshOrbit.Payments.Domain";
        var idText = $"%{id}%";
        return await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM platform.outbox WHERE event_type LIKE {type} AND payload::text LIKE {idText}")
            .SingleAsync(Ct);
    }
}
