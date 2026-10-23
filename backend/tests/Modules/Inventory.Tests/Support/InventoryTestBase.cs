using System.Net.Http.Json;
using System.Text.Json;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Infrastructure;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DomainStockUnit = FreshOrbit.Inventory.Domain.Stock.StockUnit;

namespace FreshOrbit.Inventory.Tests.Support;

/// <summary>The real Api with the Catalog and Locations Public APIs replaced by fakes.</summary>
public sealed class InventoryApiFactory(InventoryDatabase database, FakeCatalogApi catalog, FakeLocationsApi locations) : ApiFactory(database)
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ICatalogApi>(catalog));
        services.Replace(ServiceDescriptor.Singleton<ILocationsApi>(locations));
    }
}

/// <summary>A stock batch to seed: quantity, total cost, best-before offset and receipt order.</summary>
public sealed record SeedBatch(int Quantity, long TotalCostPaise, TimeSpan BestBeforeIn, string? Code = null);

public abstract class InventoryTestBase(InventoryDatabase database) : IAsyncLifetime
{
    private InventoryApiFactory? _factory;

    protected InventoryDatabase Database => database;

    protected FakeCatalogApi Catalog { get; } = new();

    protected FakeLocationsApi Locations { get; } = new();

    protected TestClock Clock { get; } = new();

    protected InventoryApiFactory Factory => _factory ??= new InventoryApiFactory(database, Catalog, Locations);

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Services wired like the Worker with the Inventory module, the fakes, <see cref="Clock"/> and an actor.</summary>
    protected ServiceProvider CreateServices(ICurrentActor? actor = null)
        => database.CreateServices(
            services =>
            {
                new InventoryModule().Register(services, new ConfigurationBuilder().Build());
                services.AddSingleton<ICatalogApi>(Catalog);
                services.AddSingleton<ILocationsApi>(Locations);
            },
            Clock,
            actor ?? TestActor.System());

    protected FreshOrbitDbContext CreateDbContext() => database.CreateDbContext(clock: Clock);

    /// <summary>Creates a stock item with batches received in the given order (one second apart), saved directly.</summary>
    protected async Task<StockItem> SeedStockItemAsync(Guid locationId, Guid variantId, DomainStockUnit unit = DomainStockUnit.Gram, int threshold = 0, params SeedBatch[] batches)
    {
        await using var db = CreateDbContext();
        var item = StockItem.Create(locationId, variantId, unit, threshold);
        var receivedAt = Clock.UtcNow.AddMinutes(-10);
        foreach (var batch in batches)
        {
            receivedAt = receivedAt.AddSeconds(1);
            item.Receive(
                new BatchReceipt(batch.Code ?? $"B-{Guid.NewGuid():N}"[..20], null, null, null, batch.Quantity, batch.TotalCostPaise, Clock.UtcNow.Add(batch.BestBeforeIn), 1.5m),
                receivedAt,
                null,
                temperatureWarning: false);
        }

        item.ClearEvents();
        db.Add(item);
        await db.SaveChangesAsync(Ct);
        return item;
    }

    protected async Task<StockItem> LoadStockItemAsync(Guid stockItemId)
    {
        await using var db = CreateDbContext();
        return await db.Set<StockItem>().AsNoTracking()
            .Include(s => s.Batches)
            .Include(s => s.Reservations)
            .AsSplitQuery()
            .SingleAsync(s => s.Id == stockItemId, Ct);
    }

    protected async Task<List<StockMovement>> LoadMovementsAsync(Guid stockItemId)
    {
        await using var db = CreateDbContext();
        return await db.Set<StockMovement>().AsNoTracking().Where(m => m.StockItemId == stockItemId).OrderBy(m => m.OccurredAt).ThenBy(m => m.Id).ToListAsync(Ct);
    }

    /// <summary>Outbox rows whose type name ends with <paramref name="eventName"/> and whose payload mentions <paramref name="id"/>.</summary>
    protected async Task<List<JsonElement>> OutboxAsync(string eventName, Guid id)
    {
        await using var db = CreateDbContext();
        var needle = id.ToString();
        var rows = await db.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.EventType.Contains("." + eventName + ","))
            .OrderBy(m => m.OccurredAt)
            .ToListAsync(Ct);
        return [.. rows.Where(r => r.Payload.Contains(needle, StringComparison.Ordinal)).Select(r => JsonDocument.Parse(r.Payload).RootElement.Clone())];
    }

    protected HttpClient StaffClient(Guid? locationId = null, params string[] permissions)
        => Factory.CreateStaffClient(
            permissions.Length == 0 ? ["inventory.view", "inventory.receive", "inventory.adjust"] : permissions,
            locationId is { } id ? [id] : null,
            allLocations: locationId is null);

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).Clone();

    protected static async Task<string?> CodeOfAsync(HttpResponseMessage response)
        => (await ReadJsonAsync(response)).GetProperty("code").GetString();
}
