using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Inventory.Application.Stock;
using FreshOrbit.Inventory.Domain.ColdChain;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Tests.Support;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Inventory.Tests.Integration;

/// <summary>Admin inventory endpoints through the real Api (docs/api §4.14).</summary>
public sealed class StockEndpointTests(InventoryDatabase database) : InventoryTestBase(database)
{
    private const string Base = "/api/v1/admin/inventory";

    [Fact]
    public async Task Create_stock_item_then_duplicate_and_unknown_variant_are_refused()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var client = StaffClient();

        var created = await client.PostAsJsonAsync($"{Base}/stock-items", new { fulfillmentLocationId = location, variantId = variant, lowStockThreshold = 1000 }, Ct);
        var duplicate = await client.PostAsJsonAsync($"{Base}/stock-items", new { fulfillmentLocationId = location, variantId = variant, lowStockThreshold = 0 }, Ct);
        var unknown = await client.PostAsJsonAsync($"{Base}/stock-items", new { fulfillmentLocationId = location, variantId = Guid.CreateVersion7(), lowStockThreshold = 0 }, Ct);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(created);
        body.GetProperty("onHand").GetProperty("unit").GetString().Should().Be("GRAM");
        created.Headers.ETag!.Tag.Should().Be($"\"{body.GetProperty("version").GetString()}\"");
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOfAsync(duplicate)).Should().Be("DUPLICATE");
        unknown.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadJsonAsync(unknown)).GetProperty("errors").TryGetProperty("variantId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task I7_receipt_replayed_with_the_same_key_creates_one_batch()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant);
        var client = StaffClient();
        var body = Receipt(location, variant, "SEER-261023-01", 24_500, 2_131_500, 1.5m);

        var first = await SendAsync(client, HttpMethod.Post, $"{Base}/receipts", body, idempotencyKey: "rcpt-1");
        var replay = await SendAsync(client, HttpMethod.Post, $"{Base}/receipts", body, idempotencyKey: "rcpt-1");

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.GetValues("Idempotent-Replayed").Should().Equal("true");
        var json = await ReadJsonAsync(first);
        json.GetProperty("batch").GetProperty("costPerUnitPaise").GetInt64().Should().Be(87);
        json.GetProperty("movement").GetProperty("kind").GetString().Should().Be("RECEIPT");
        json.GetProperty("movement").GetProperty("costPaise").GetInt64().Should().Be(2_131_500);
        json.GetProperty("temperatureWarning").GetBoolean().Should().BeFalse();
        var saved = await LoadStockItemAsync(item.Id);
        saved.Batches.Should().ContainSingle();
        saved.OnHand.Should().Be(24_500);
        (await LoadMovementsAsync(item.Id)).Should().ContainSingle(m => m.Kind == MovementKind.Receipt);
        (await OutboxAsync("StockReceived", item.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task I8_receipt_with_a_batch_code_used_at_the_location_is_refused()
    {
        var (location, variant, other) = (Locations.AddLocation(), Catalog.AddVariant(), Catalog.AddVariant());
        await SeedStockItemAsync(location, variant);
        await SeedStockItemAsync(location, other);
        var client = StaffClient();

        await SendAsync(client, HttpMethod.Post, $"{Base}/receipts", Receipt(location, variant, "DUP-1", 1000, 1000, null), idempotencyKey: Guid.NewGuid().ToString());
        var duplicate = await SendAsync(client, HttpMethod.Post, $"{Base}/receipts", Receipt(location, other, "DUP-1", 1000, 1000, null), idempotencyKey: Guid.NewGuid().ToString());

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOfAsync(duplicate)).Should().Be("BATCH_CODE_TAKEN");
    }

    [Fact]
    public async Task I14_receipt_above_4_degrees_is_accepted_with_a_warning_and_an_out_of_range_log()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        await SeedStockItemAsync(location, variant);

        var response = await SendAsync(StaffClient(), HttpMethod.Post, $"{Base}/receipts", Receipt(location, variant, "WARM-1", 1000, 50_000, 5.2m), idempotencyKey: Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJsonAsync(response)).GetProperty("temperatureWarning").GetBoolean().Should().BeTrue();
        await using var db = CreateDbContext();
        var log = await db.Set<ColdChainLog>().SingleAsync(l => l.FulfillmentLocationId == location, Ct);
        (log.StorageUnit, log.TemperatureC, log.IsOutOfRange).Should().Be(("RECEIPT", 5.2m, true));
        (await OutboxAsync("ColdChainBreachRecorded", location)).Should().ContainSingle();
    }

    [Fact]
    public async Task Receipt_refusals_missing_stock_item_and_wrong_unit()
    {
        var (location, variant, unknown) = (Locations.AddLocation(), Catalog.AddVariant(), Catalog.AddVariant());
        await SeedStockItemAsync(location, variant);
        var client = StaffClient();

        var missing = await SendAsync(client, HttpMethod.Post, $"{Base}/receipts", Receipt(location, unknown, "X-1", 1000, 1000, null), idempotencyKey: Guid.NewGuid().ToString());
        var wrongUnit = await SendAsync(client, HttpMethod.Post, $"{Base}/receipts", Receipt(location, variant, "X-2", 1000, 1000, null, "PIECE"), idempotencyKey: Guid.NewGuid().ToString());

        (await CodeOfAsync(missing)).Should().Be("STOCK_ITEM_MISSING");
        wrongUnit.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadJsonAsync(wrongUnit)).GetProperty("errors").TryGetProperty("receivedQuantity.unit", out _).Should().BeTrue();
    }

    [Fact]
    public async Task I4_waste_records_the_proportional_cost_and_WasteRecorded()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(10_000, 845_000, TimeSpan.FromDays(2)));

        var response = await SendAsync(StaffClient(), HttpMethod.Post, $"{Base}/waste", new
        {
            stockItemId = item.Id,
            quantity = new { value = 333, unit = "GRAM" },
            reasonCode = "SPOILED",
            notes = "Soft flesh",
        }, idempotencyKey: Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var movement = (await LoadMovementsAsync(item.Id)).Single(m => m.Kind == MovementKind.Waste);
        (movement.Quantity, movement.CostPaise, movement.ReasonCode, movement.ReferenceType).Should().Be((-333, -28_139L, "SPOILED", MovementReferenceType.Manual));
        movement.RecordedBy.Should().NotBeNull();
        var wasted = (await OutboxAsync("WasteRecorded", item.Id)).Should().ContainSingle().Subject;
        wasted.GetProperty("totalCostPaise").GetInt64().Should().Be(-28_139);
    }

    [Fact]
    public async Task U20_invalid_reason_code_is_refused()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(1000, 1000, TimeSpan.FromDays(2)));

        var response = await SendAsync(StaffClient(), HttpMethod.Post, $"{Base}/waste", new
        {
            stockItemId = item.Id,
            quantity = new { value = 10, unit = "GRAM" },
            reasonCode = "STOLEN_BY_CAT",
        }, idempotencyKey: Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOfAsync(response)).Should().Be("REASON_CODE_INVALID");
    }

    [Fact]
    public async Task I9_adjustment_needs_the_current_version()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(1000, 100_000, TimeSpan.FromDays(2)));
        var client = StaffClient();
        var etag = (await client.GetAsync($"{Base}/stock/{item.Id}", Ct)).Headers.ETag!.Tag;
        var body = new { stockItemId = item.Id, kind = "ADJUSTMENT_OUT", quantity = new { value = 100, unit = "GRAM" }, reasonCode = "LOST" };

        var missing = await SendAsync(client, HttpMethod.Post, $"{Base}/adjustments", body);
        var current = await SendAsync(client, HttpMethod.Post, $"{Base}/adjustments", body, ifMatch: etag);
        var stale = await SendAsync(client, HttpMethod.Post, $"{Base}/adjustments", body, ifMatch: etag);

        missing.StatusCode.Should().Be((HttpStatusCode)428);
        (await CodeOfAsync(missing)).Should().Be("VERSION_REQUIRED");
        current.StatusCode.Should().Be(HttpStatusCode.OK);
        current.Headers.ETag!.Tag.Should().NotBe(etag);
        var change = await ReadJsonAsync(current);
        change.GetProperty("movements")[0].GetProperty("costPaise").GetInt64().Should().Be(-10_000);
        change.GetProperty("stockItem").GetProperty("onHand").GetProperty("value").GetInt32().Should().Be(900);
        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        (await CodeOfAsync(stale)).Should().Be("CONCURRENCY_CONFLICT");
        (await OutboxAsync("StockAdjusted", item.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task I10_two_adjustments_quoting_the_same_version_one_saves_the_other_loses_the_race()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(1000, 100_000, TimeSpan.FromDays(2)));
        await using var services = CreateServices(TestActor.Staff(allLocations: true));
        var version = (await LoadStockItemAsync(item.Id)).Version;
        var request = new AdjustStockRequest(item.Id, "ADJUSTMENT_IN", null, new QuantityInput(50, "GRAM"), "FOUND", null);
        await using var slow = services.CreateAsyncScope();

        // The slow request has read the item (and passed its If-Match check) before the fast one saves.
        await slow.ServiceProvider.GetRequiredService<IStockItemRepository>().GetWithActiveChildrenAsync(item.Id, null, Ct);
        await using (var fast = services.CreateAsyncScope())
        {
            await fast.ServiceProvider.GetRequiredService<StockCommandService>().AdjustAsync(version, request, Ct);
        }

        var lost = () => slow.ServiceProvider.GetRequiredService<StockCommandService>().AdjustAsync(version, request, Ct);

        await lost.Should().ThrowAsync<ConcurrentSaveException>();
        (await LoadStockItemAsync(item.Id)).OnHand.Should().Be(1050);
    }

    [Fact]
    public async Task Count_equal_to_on_hand_returns_200_without_changing_the_version()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(1000, 100_000, TimeSpan.FromDays(2)));
        var client = StaffClient();
        var etag = (await client.GetAsync($"{Base}/stock/{item.Id}", Ct)).Headers.ETag!.Tag;

        var same = await SendAsync(client, HttpMethod.Post, $"{Base}/counts", new { stockItemId = item.Id, countedQuantity = new { value = 1000, unit = "GRAM" } }, ifMatch: etag);
        var lower = await SendAsync(client, HttpMethod.Post, $"{Base}/counts", new { stockItemId = item.Id, countedQuantity = new { value = 950, unit = "GRAM" } }, ifMatch: etag);

        same.StatusCode.Should().Be(HttpStatusCode.OK);
        same.Headers.ETag!.Tag.Should().Be(etag);
        (await ReadJsonAsync(same)).GetProperty("movements").GetArrayLength().Should().Be(0);
        lower.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoadMovementsAsync(item.Id)).Should().ContainSingle(m => m.Kind == MovementKind.CountCorrection && m.Quantity == -50);
    }

    [Fact]
    public async Task Discard_writes_off_the_batch_and_reads_show_it()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: [new SeedBatch(400, 40_000, TimeSpan.FromDays(2), "D-1"), new SeedBatch(600, 60_000, TimeSpan.FromDays(3), "D-2")]);
        var client = StaffClient();
        var batches = await ReadJsonAsync(await client.GetAsync($"{Base}/batches?stockItemId={item.Id}&status=ACTIVE", Ct));
        var first = batches.GetProperty("items")[0];
        var url = $"{Base}/batches/{first.GetProperty("id").GetGuid()}/discard";

        var discarded = await SendAsync(client, HttpMethod.Post, url, new { reasonCode = "QC_REJECTED", notes = "Smell" }, ifMatch: $"\"{first.GetProperty("stockItemVersion").GetString()}\"");
        var again = await SendAsync(client, HttpMethod.Post, url, new { reasonCode = "QC_REJECTED" }, ifMatch: discarded.Headers.ETag!.Tag);

        batches.GetProperty("totalCount").GetInt32().Should().Be(2);
        first.GetProperty("batchCode").GetString().Should().Be("D-1");
        discarded.StatusCode.Should().Be(HttpStatusCode.OK);
        again.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOfAsync(again)).Should().Be("BATCH_NOT_ACTIVE");
        var movements = await ReadJsonAsync(await client.GetAsync($"{Base}/movements?stockItemId={item.Id}&kind=WASTE", Ct));
        movements.GetProperty("items")[0].GetProperty("costPaise").GetInt64().Should().Be(-40_000);
        var stock = await ReadJsonAsync(await client.GetAsync($"{Base}/stock/{item.Id}", Ct));
        stock.GetProperty("onHand").GetProperty("value").GetInt32().Should().Be(600);
        stock.GetProperty("batches").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Threshold_update_and_low_stock_list()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        var item = await SeedStockItemAsync(location, variant, batches: new SeedBatch(800, 80_000, TimeSpan.FromDays(2)));
        var client = StaffClient();
        var etag = (await client.GetAsync($"{Base}/stock/{item.Id}", Ct)).Headers.ETag!.Tag;

        var updated = await SendAsync(client, HttpMethod.Put, $"{Base}/stock/{item.Id}/threshold", new { lowStockThreshold = 1000 }, ifMatch: etag);
        var low = await ReadJsonAsync(await client.GetAsync($"{Base}/stock?fulfillmentLocationId={location}&lowStockOnly=true", Ct));

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(updated)).GetProperty("isLowStock").GetBoolean().Should().BeTrue();
        low.GetProperty("totalCount").GetInt32().Should().Be(1);
        (await OutboxAsync("LowStockReached", item.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task I15_staff_scoped_to_one_location_see_only_it_and_are_refused_elsewhere()
    {
        var (a, b) = (Locations.AddLocation(), Locations.AddLocation());
        var itemA = await SeedStockItemAsync(a, Catalog.AddVariant());
        var itemB = await SeedStockItemAsync(b, Catalog.AddVariant(), batches: new SeedBatch(100, 100, TimeSpan.FromDays(2)));
        var client = StaffClient(a);

        var list = await ReadJsonAsync(await client.GetAsync($"{Base}/stock?pageSize=100", Ct));
        var detail = await client.GetAsync($"{Base}/stock/{itemB.Id}", Ct);
        var waste = await SendAsync(client, HttpMethod.Post, $"{Base}/waste", new { stockItemId = itemB.Id, quantity = new { value = 10, unit = "GRAM" }, reasonCode = "DAMAGED" }, idempotencyKey: Guid.NewGuid().ToString());

        list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("fulfillmentLocationId").GetGuid()).Should().OnlyContain(id => id == a).And.Contain(a);
        list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Contain(itemA.Id);
        detail.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOfAsync(detail)).Should().Be("LOCATION_ACCESS_DENIED");
        waste.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOfAsync(waste)).Should().Be("LOCATION_ACCESS_DENIED");
    }

    [Fact]
    public async Task Cold_chain_entry_is_flagged_by_the_server_and_listed()
    {
        var location = Locations.AddLocation();
        var client = StaffClient();

        var breach = await client.PostAsJsonAsync($"{Base}/cold-chain", new { fulfillmentLocationId = location, storageUnit = "FREEZER", temperatureC = -17.5m }, Ct);
        var fine = await client.PostAsJsonAsync($"{Base}/cold-chain", new { fulfillmentLocationId = location, storageUnit = "CHILLER", temperatureC = 2.0m }, Ct);
        var unknownUnit = await client.PostAsJsonAsync($"{Base}/cold-chain", new { fulfillmentLocationId = location, storageUnit = "OVEN", temperatureC = 2.0m }, Ct);
        var list = await ReadJsonAsync(await client.GetAsync($"{Base}/cold-chain?fulfillmentLocationId={location}&outOfRangeOnly=true", Ct));

        breach.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJsonAsync(breach)).GetProperty("isOutOfRange").GetBoolean().Should().BeTrue();
        (await ReadJsonAsync(fine)).GetProperty("isOutOfRange").GetBoolean().Should().BeFalse();
        unknownUnit.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        list.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Reservations_list_shows_active_reservations()
    {
        var (location, variant) = (Locations.AddLocation(), Catalog.AddVariant());
        await SeedStockItemAsync(location, variant, batches: new SeedBatch(1000, 1000, TimeSpan.FromDays(2)));
        await using (var services = CreateServices())
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Public.IInventoryApi>().ReserveAsync(new Public.ReserveStockRequest(Guid.CreateVersion7(), location, [new(Guid.CreateVersion7(), variant, 250)], null), Ct);
            await scope.ServiceProvider.GetRequiredService<SharedKernel.Persistence.IUnitOfWork>().SaveChangesAsync(Ct);
        }

        var list = await ReadJsonAsync(await StaffClient().GetAsync($"{Base}/reservations?fulfillmentLocationId={location}", Ct));

        list.GetProperty("items").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("quantity").GetProperty("value").GetInt32().Should().Be(250);
    }

    private static object Receipt(Guid location, Guid variant, string code, int quantity, long cost, decimal? temperature, string unit = "GRAM") => new
    {
        fulfillmentLocationId = location,
        variantId = variant,
        batchCode = code,
        supplierName = "Murugan Fisheries",
        sourceLandingCenter = "Kasimedu Fishing Harbour",
        caughtOrHarvestedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
        receivedQuantity = new { value = quantity, unit },
        totalCostPaise = cost,
        bestBeforeAt = DateTimeOffset.UtcNow.AddDays(2),
        receiptTemperatureC = temperature,
    };

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, object body, string? ifMatch = null, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, options: new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request, Ct);
    }
}
