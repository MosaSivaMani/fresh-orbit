using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Ordering.Tests.Integration;

/// <summary>An API host per test class with a fresh <see cref="CheckoutWorld"/> and helpers for orders and outbox rows.</summary>
public abstract class OrderingTestBase : IAsyncLifetime
{
    protected static readonly DateTimeOffset Start = new(2026, 10, 23, 3, 0, 0, TimeSpan.Zero);

    protected OrderingTestBase(OrderingDatabase database)
    {
        Database = database;
        World = new CheckoutWorld(Start);
        Factory = new OrderingApiFactory(database, World);
        Factory.Clock.Set(Start);
        CustomerId = Guid.CreateVersion7();
        Customer = ClientFor(f => f.CreateCustomerClient(customerId: CustomerId));
    }

    protected OrderingDatabase Database { get; }

    protected CheckoutWorld World { get; }

    protected OrderingApiFactory Factory { get; }

    protected Guid CustomerId { get; }

    protected HttpClient Customer { get; }

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var db = Database.CreateDbContext();
        await World.EnsureProbeAsync(db);
    }

    public async ValueTask DisposeAsync()
    {
        Customer.Dispose();
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected object PlaceBody(string method = "COD", long? expectedTotal = null, string preference = "CALL_ME") => new
    {
        addressId = World.AddressId,
        deliverySlotId = World.SlotId,
        paymentMethod = method,
        substitutionPreference = preference,
        customerNotes = "Please call before arriving.",
        expectedTotalPaise = expectedTotal ?? World.EstimatedTotalPaise,
    };

    protected async Task<HttpResponseMessage> PlaceAsync(object body, string? key = null, HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await (client ?? Customer).SendAsync(request, Ct);
    }

    /// <summary>Places a COD (or <paramref name="method"/>) order and returns its id.</summary>
    protected async Task<Guid> PlaceOrderAsync(string method = "COD", string preference = "CALL_ME")
    {
        var response = await PlaceAsync(PlaceBody(method, preference: preference));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Creates a client whose token is issued at the real time: JWT lifetime is validated against the wall clock, while
    /// the business clock stays at the test's fixed instant.
    /// </summary>
    protected HttpClient ClientFor(Func<OrderingApiFactory, HttpClient> create)
    {
        var testNow = Factory.Clock.UtcNow;
        Factory.Clock.Set(DateTimeOffset.UtcNow);
        try
        {
            return create(Factory);
        }
        finally
        {
            Factory.Clock.Set(testNow);
        }
    }

    protected HttpClient Staff(params string[] permissions)
        => ClientFor(f => f.CreateStaffClient(permissions.Length == 0 ? ["orders.view", "orders.pack", "orders.cancel", "refunds.request", "complaints.manage"] : permissions, [CheckoutWorld.LocationId]));

    protected static async Task<HttpResponseMessage> SendWithVersionAsync(HttpClient client, HttpMethod method, string url, string? etag, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (etag is not null)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue(etag));
        }

        return await client.SendAsync(request, Ct);
    }

    protected static async Task<string> ETagOfAsync(HttpClient staff, Guid orderId)
    {
        var response = await staff.GetAsync(new Uri($"/api/v1/admin/orders/{orderId}", UriKind.Relative), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return response.Headers.ETag!.Tag;
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).Clone();

    protected static string CodeOf(JsonElement problem) => problem.GetProperty("code").GetString()!;

    protected async Task<Order?> LoadOrderAsync(Guid orderId)
    {
        await using var db = Database.CreateDbContext();
        return await db.Set<Order>().AsNoTracking().Include(o => o.Items).Include(o => o.Adjustments).Include(o => o.Substitutions).FirstOrDefaultAsync(o => o.Id == orderId, Ct);
    }

    protected async Task<List<JsonElement>> OutboxAsync(string eventName, Guid orderId)
    {
        await using var db = Database.CreateDbContext();
        var type = "%." + eventName + ",%";
        var id = "%" + orderId + "%";
        var rows = await db.Database.SqlQuery<string>($"""
            SELECT payload::text AS "Value" FROM platform.outbox
            WHERE event_type LIKE {type} AND payload::text LIKE {id}
            ORDER BY occurred_at, id
            """).ToListAsync(Ct);
        return [.. rows.Select(r => JsonDocument.Parse(r).RootElement.Clone())];
    }

    protected async Task<T> InScopeAsync<T>(Func<IServiceProvider, FreshOrbitDbContext, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FreshOrbitDbContext>();
        return await action(scope.ServiceProvider, db);
    }

    /// <summary>Runs an event handler body the way the outbox dispatcher does: one transaction, one save.</summary>
    protected Task<T> AsHandlerAsync<T>(Func<IServiceProvider, Task<T>> handle)
        => InScopeAsync(async (services, db) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            var result = await handle(services);
            await db.SaveChangesAsync(Ct);
            await transaction.CommitAsync(Ct);
            return result;
        });
}
