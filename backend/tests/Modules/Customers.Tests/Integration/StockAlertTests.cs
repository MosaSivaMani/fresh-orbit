using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Customers.Domain.StockAlerts;
using FreshOrbit.E1Testing;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Notifications.Domain.Messages;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CatalogStockUnit = FreshOrbit.Catalog.Public.StockUnit;
using InventoryStockUnit = FreshOrbit.Inventory.Domain.Stock.StockUnit;

namespace FreshOrbit.Customers.Tests.Integration;

/// <summary>Catalog.Public stand-in: every asked variant is an active, sold "Seer fish" variant unless marked not sold.</summary>
public sealed class StockAlertCatalog : ICatalogApi
{
    public ConcurrentDictionary<Guid, bool> NotSold { get; } = new();

    public Task<IReadOnlyList<VariantInfo>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<VariantInfo>>([.. variantIds.Select(id => new VariantInfo(
            id, Guid.CreateVersion7(), Guid.CreateVersion7(), "Seer fish", "seer-fish", "Curry cut", $"SKU-{id:N}", null, "0302", "SEAFOOD_FRESH_CHILLED",
            SellUnit.Kg, CatalogStockUnit.Gram, null, null, 250, 5000, 250, true, !NotSold.ContainsKey(id), [])
        {
            ProductStatus = ProductStatus.Active,
        })]);
}

/// <summary>
/// CUS-R10 back-in-stock alerts through the real Api: real Locations and Inventory (stock items saved through the
/// shared DbContext), Catalog replaced by <see cref="StockAlertCatalog"/>, and the outbox dispatched as the Worker does.
/// </summary>
public sealed class StockAlertTests : E1ApiTestBase
{
    private readonly StockAlertCatalog _catalog = new();

    public StockAlertTests(E1Database database)
        : base(database)
        => Factory.AdditionalServices = services => services.Replace(ServiceDescriptor.Singleton<ICatalogApi>(_catalog));

    [Fact]
    public async Task Alert_is_created_once_then_returned_again_and_listed()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);
        var locationId = await CreateLocationAsync();
        var variantId = Guid.CreateVersion7();

        var created = await client.PostAsJsonAsync("/api/v1/me/stock-alerts", new { variantId, fulfillmentLocationId = locationId }, Ct);
        var again = await client.PostAsJsonAsync("/api/v1/me/stock-alerts", new { variantId, fulfillmentLocationId = locationId }, Ct);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await ReadJsonAsync(created);
        var second = await ReadJsonAsync(again);
        second.GetProperty("id").GetGuid().Should().Be(first.GetProperty("id").GetGuid());
        first.GetProperty("variantId").GetGuid().Should().Be(variantId);
        first.GetProperty("fulfillmentLocationId").GetGuid().Should().Be(locationId);

        var list = await ReadJsonAsync(await client.GetAsync("/api/v1/me/stock-alerts", Ct));
        list.EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).Should().Equal(first.GetProperty("id").GetGuid());

        var alertId = first.GetProperty("id").GetGuid();
        (await client.DeleteAsync($"/api/v1/me/stock-alerts/{alertId}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/v1/me/stock-alerts/{alertId}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadJsonAsync(await client.GetAsync("/api/v1/me/stock-alerts", Ct))).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Alert_is_refused_while_the_variant_is_in_stock_or_not_sold_or_the_location_is_unknown()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);
        var locationId = await CreateLocationAsync();
        var inStock = Guid.CreateVersion7();
        await SaveStockItemAsync(locationId, inStock, receivedQuantity: 1000);
        var notSold = Guid.CreateVersion7();
        _catalog.NotSold[notSold] = true;

        var available = await client.PostAsJsonAsync("/api/v1/me/stock-alerts", new { variantId = inStock, fulfillmentLocationId = locationId }, Ct);
        var inactive = await client.PostAsJsonAsync("/api/v1/me/stock-alerts", new { variantId = notSold, fulfillmentLocationId = locationId }, Ct);
        var unknownLocation = await client.PostAsJsonAsync("/api/v1/me/stock-alerts", new { variantId = Guid.CreateVersion7(), fulfillmentLocationId = Guid.CreateVersion7() }, Ct);

        available.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorCodeAsync(available)).Should().Be("STOCK_AVAILABLE");
        inactive.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorCodeAsync(inactive)).Should().Be("PRODUCT_NOT_AVAILABLE");
        var unknown = await ReadJsonAsync(unknownLocation);
        unknown.GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
        unknown.GetProperty("errors").TryGetProperty("fulfillmentLocationId", out _).Should().BeTrue();
        (await QueryAsync(db => db.Set<StockAlert>().CountAsync(a => a.FulfillmentLocationId == locationId, Ct))).Should().Be(0);
    }

    [Fact]
    public async Task The_fifty_first_alert_is_refused()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);
        var locationId = await CreateLocationAsync();
        for (var i = 0; i < StockAlert.MaxPerCustomer; i++)
        {
            (await client.PostAsJsonAsync("/api/v1/me/stock-alerts", new { variantId = Guid.CreateVersion7(), fulfillmentLocationId = locationId }, Ct))
                .StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var refused = await client.PostAsJsonAsync("/api/v1/me/stock-alerts", new { variantId = Guid.CreateVersion7(), fulfillmentLocationId = locationId }, Ct);

        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorCodeAsync(refused)).Should().Be("STOCK_ALERT_LIMIT_REACHED");
        (await ReadJsonAsync(await client.GetAsync("/api/v1/me/stock-alerts", Ct))).GetArrayLength().Should().Be(StockAlert.MaxPerCustomer);
    }

    [Fact]
    public async Task Stock_coming_back_fires_the_alert_once_queues_BACK_IN_STOCK_and_removes_the_alert()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);
        var locationId = await CreateLocationAsync();
        var variantId = Guid.CreateVersion7();
        var stockItemId = await SaveStockItemAsync(locationId, variantId, receivedQuantity: null);
        var created = await client.PostAsJsonAsync("/api/v1/me/stock-alerts", new { variantId, fulfillmentLocationId = locationId }, Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        // Stock 0 → 1 000 g: Inventory raises StockAvailableAgain in the same save as the receipt movement.
        await InScopeAsync(async provider =>
        {
            var db = provider.GetRequiredService<FreshOrbitDbContext>();
            var item = await db.Set<StockItem>().SingleAsync(s => s.Id == stockItemId, Ct);
            item.Receive(new BatchReceipt("BACK-1", null, null, null, 1000, 100_000, Factory.Clock.UtcNow.AddDays(2), null), Factory.Clock.UtcNow, null, false);
            return await db.SaveChangesAsync(Ct);
        });
        var raised = (await QueryAsync(db => db.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.EventType.Contains(".StockAvailableAgain,"))
            .ToListAsync(Ct))).Where(m => m.Payload.Contains(variantId.ToString(), StringComparison.Ordinal)).ToList();
        raised.Should().ContainSingle();

        await DispatchOutboxAsync();

        var processed = await QueryAsync(db => db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.Id == raised[0].Id, Ct));
        processed.ProcessedAt.Should().NotBeNull(processed.Error);
        (await QueryAsync(db => db.Set<StockAlert>().CountAsync(a => a.VariantId == variantId, Ct))).Should().Be(0);
        (await ReadJsonAsync(await client.GetAsync("/api/v1/me/stock-alerts", Ct))).GetArrayLength().Should().Be(0);

        var messages = await QueryAsync(db => db.Set<Message>().AsNoTracking()
            .Where(m => m.UserId == session.UserId && m.TemplateCode == "BACK_IN_STOCK")
            .ToListAsync(Ct));
        messages.Should().Contain(m => m.Channel == Channel.InApp);
        messages.Should().NotContain(m => m.Channel == Channel.Sms);
        messages.Should().OnlyContain(m => m.Payload["productName"] == "Seer fish" && m.Payload["variantName"] == "Curry cut");
        messages.GroupBy(m => m.Channel).Should().OnlyContain(g => g.Count() == 1, "one notification per channel, then the alert is gone");
    }

    private async Task DispatchOutboxAsync()
    {
        var dispatcher = Factory.Services.GetRequiredService<OutboxDispatcher>();
        while (await dispatcher.DispatchBatchAsync(Ct) > 0)
        {
        }
    }

    /// <summary>A stock item for the variant at the location, optionally with one received batch (saved in one save).</summary>
    private Task<Guid> SaveStockItemAsync(Guid locationId, Guid variantId, int? receivedQuantity)
        => InScopeAsync(async provider =>
        {
            var db = provider.GetRequiredService<FreshOrbitDbContext>();
            var item = StockItem.Create(locationId, variantId, InventoryStockUnit.Gram, 0);
            if (receivedQuantity is { } quantity)
            {
                item.Receive(new BatchReceipt("FIRST-1", null, null, null, quantity, 100_000, Factory.Clock.UtcNow.AddDays(2), null), Factory.Clock.UtcNow, null, false);
            }

            db.Add(item);
            await db.SaveChangesAsync(Ct);
            return item.Id;
        });

    /// <summary>A city and a fulfillment location through the admin endpoints (status does not matter for alerts).</summary>
    private async Task<Guid> CreateLocationAsync()
    {
        using var admin = Factory.CreateStaffClient(["locations.manage"], allLocations: true);
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var city = await admin.PostAsJsonAsync("/api/v1/admin/cities", new { code = $"CITY_{suffix}", name = "Alert City", stateCode = "TN", timeZone = "Asia/Kolkata" }, Ct);
        var cityJson = await ReadJsonAsync(city);
        city.IsSuccessStatusCode.Should().BeTrue($"city creation answered {cityJson}");
        var location = await admin.PostAsJsonAsync("/api/v1/admin/fulfillment-locations", new
        {
            cityId = cityJson.GetProperty("id").GetGuid(),
            code = $"LOC-{suffix}",
            name = "Alert Store",
            addressLine = "1 Beach Road",
            pincode = "600041",
            phone = "+914400000000",
            latitude = 12.98m,
            longitude = 80.26m,
            hasColdStorage = true,
            fssaiLicenseNo = "12345678901234",
        }, Ct);
        var locationJson = await ReadJsonAsync(location);
        location.IsSuccessStatusCode.Should().BeTrue($"location creation answered {locationJson}");
        return locationJson.GetProperty("id").GetGuid();
    }
}
