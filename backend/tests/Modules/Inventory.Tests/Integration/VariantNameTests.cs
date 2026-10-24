using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Inventory.Tests.Integration;

/// <summary>G3: inventory admin reads carry the Catalog product name, variant name and SKU.</summary>
public sealed class VariantNameTests(InventoryDatabase database) : InventoryTestBase(database)
{
    private const string Base = "/api/v1/admin/inventory";

    [Fact]
    public async Task Stock_batches_movements_and_reservations_carry_variant_names()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, threshold: 0, batches: new SeedBatch(5_000, 435_000, TimeSpan.FromDays(2)));
        await ReserveAsync(item.Id, 1_000);
        var sku = $"SKU-{variant:N}";
        var client = StaffClient(location);

        var stock = Single(await GetAsync(client, $"{Base}/stock?fulfillmentLocationId={location}"));
        var detail = await GetAsync(client, $"{Base}/stock/{item.Id}");
        var batch = Single(await GetAsync(client, $"{Base}/batches?stockItemId={item.Id}"));
        var movement = Single(await GetAsync(client, $"{Base}/movements?stockItemId={item.Id}"));
        var reservation = Single(await GetAsync(client, $"{Base}/reservations?fulfillmentLocationId={location}"));

        foreach (var row in new[] { stock, detail, detail.GetProperty("batches")[0], batch, movement, reservation })
        {
            row.GetProperty("productName").GetString().Should().Be("Seer fish");
            row.GetProperty("variantName").GetString().Should().Be("Curry cut");
            row.GetProperty("sku").GetString().Should().Be(sku);
        }

        movement.GetProperty("variantId").GetGuid().Should().Be(variant);
    }

    [Fact]
    public async Task Rows_of_a_variant_unknown_to_catalog_have_null_names()
    {
        var location = Locations.AddLocation();
        var item = await SeedStockItemAsync(location, Guid.CreateVersion7());

        var stock = Single(await GetAsync(StaffClient(location), $"{Base}/stock?fulfillmentLocationId={location}"));

        stock.GetProperty("id").GetGuid().Should().Be(item.Id);
        stock.GetProperty("productName").ValueKind.Should().Be(JsonValueKind.Null);
        stock.GetProperty("sku").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private async Task ReserveAsync(Guid stockItemId, int quantity)
    {
        await using var db = CreateDbContext();
        var item = await db.Set<StockItem>().Include(s => s.Reservations).SingleAsync(s => s.Id == stockItemId, Ct);
        item.Reserve(Guid.CreateVersion7(), Guid.CreateVersion7(), quantity, null);
        item.ClearEvents();
        await db.SaveChangesAsync(Ct);
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        var json = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url} answered {json}");
        return json;
    }

    private static JsonElement Single(JsonElement page) => page.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
}
