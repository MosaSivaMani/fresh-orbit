using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Delivery.Public;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.Ordering.Application.Events;
using FreshOrbit.Ordering.Domain.Complaints;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Infrastructure.Jobs;
using FreshOrbit.SharedKernel.Storage;
using FreshOrbit.SharedKernel.Web.RateLimiting;
using FreshOrbit.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SkiaSharp;

namespace FreshOrbit.Ordering.Tests.Integration;

/// <summary>Admin order gaps G13, G14, G15, G17, G18 and complaint photos W12 through the real Api.</summary>
public sealed class AdminOrderGapsTests(OrderingDatabase database) : OrderingTestBase(database)
{
    [Fact]
    public async Task G13_G14_list_rows_carry_the_version_and_status_takes_a_list()
    {
        var confirmed = await PlaceOrderAsync();
        var preparing = await PlaceOrderAsync();
        var staff = Staff();
        await StartPreparingAsync(staff, preparing);
        var slot = $"/api/v1/admin/orders?slotId={World.SlotId}";

        var repeated = await ListIdsAsync(staff, $"{slot}&status=CONFIRMED&status=PREPARING");
        var comma = await ListIdsAsync(staff, $"{slot}&status=CONFIRMED,PREPARING");
        var one = await ListIdsAsync(staff, $"{slot}&status=PREPARING");

        repeated.Keys.Should().BeEquivalentTo([confirmed, preparing]);
        comma.Keys.Should().BeEquivalentTo([confirmed, preparing]);
        one.Keys.Should().BeEquivalentTo([preparing]);
        ('"' + repeated[preparing] + '"').Should().Be(await ETagOfAsync(staff, preparing));

        var unknown = await staff.GetAsync(new Uri($"{slot}&status=CONFIRMED,SHIPPED", UriKind.Relative), Ct);
        unknown.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await ReadJsonAsync(unknown);
        CodeOf(problem).Should().Be("VALIDATION_FAILED");
        problem.GetProperty("errors").TryGetProperty("status", out _).Should().BeTrue();
    }

    [Fact]
    public async Task G17_admin_detail_lists_the_batches_each_line_was_taken_from()
    {
        var orderId = await PlaceOrderAsync();
        var order = await LoadOrderAsync(orderId);
        var fish = order!.Items.Single(i => i.LineNo == 1);
        var crabs = order.Items.Single(i => i.LineNo == 2);
        var batchId = Guid.CreateVersion7();
        var movementId = Guid.CreateVersion7();
        World.Stock = id => new OrderStock(
            id,
            [new OrderStockReservation(Guid.CreateVersion7(), fish.Id, fish.VariantId, 625, QuantityUnit.Gram, "CONSUMED", null)],
            [new OrderStockSale(movementId, fish.Id, fish.VariantId, batchId, "B-2610-01", -625, 40000, Start.AddHours(1))]);

        var detail = await ReadJsonAsync(await Staff().GetAsync(new Uri($"/api/v1/admin/orders/{orderId}", UriKind.Relative), Ct));

        var lines = detail.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("orderItemId").GetGuid());
        lines[fish.Id].GetProperty("reservationStatus").GetString().Should().Be("CONSUMED");
        var batch = lines[fish.Id].GetProperty("batches").EnumerateArray().Should().ContainSingle().Subject;
        batch.GetProperty("batchId").GetGuid().Should().Be(batchId);
        batch.GetProperty("batchCode").GetString().Should().Be("B-2610-01");
        batch.GetProperty("quantity").GetProperty("value").GetInt32().Should().Be(625);
        batch.GetProperty("quantity").GetProperty("unit").GetString().Should().Be("GRAM");
        lines[crabs.Id].GetProperty("batches").GetArrayLength().Should().Be(0);
        lines[crabs.Id].GetProperty("reservationStatus").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task G18_pick_list_has_slot_times_order_count_and_sku()
    {
        await PlaceOrderAsync();
        await PlaceOrderAsync();
        var startAt = Start.AddHours(5);
        World.Delivery.GetSlotAsync(World.SlotId, CheckoutWorld.LocationId, Arg.Any<CancellationToken>())
            .Returns(new SlotView(World.SlotId, CheckoutWorld.LocationId, new DateOnly(2026, 10, 23), startAt, startAt.AddHours(2), startAt.AddHours(-2), 10, 2, "AVAILABLE"));

        var pickList = await ReadJsonAsync(await Staff().GetAsync(new Uri($"/api/v1/admin/orders/pick-list?fulfillmentLocationId={CheckoutWorld.LocationId}&slotId={World.SlotId}", UriKind.Relative), Ct));

        pickList.GetProperty("orderCount").GetInt32().Should().Be(2);
        pickList.GetProperty("slot").GetProperty("slotDate").GetString().Should().Be("2026-10-23");
        pickList.GetProperty("slot").GetProperty("startAt").GetDateTimeOffset().Should().Be(startAt);
        pickList.GetProperty("slot").GetProperty("endAt").GetDateTimeOffset().Should().Be(startAt.AddHours(2));
        pickList.GetProperty("lines").EnumerateArray().Should().OnlyContain(l => l.GetProperty("sku").GetString() == "SKU" && l.GetProperty("orderCount").GetInt32() == 2);
    }

    [Fact]
    public async Task G15_complaint_shows_the_line_names_and_the_refund_bound_to_staff_and_customer()
    {
        var (orderId, fish) = await DeliveredOrderAsync();
        var complaint = await RaiseAsync(orderId, fish.Id);
        var complaintId = complaint.GetProperty("id").GetGuid();
        var expected = (fish.FinalTotalPaise ?? 0) - (fish.FinalDiscountPaise ?? 0);

        complaint.GetProperty("productName").GetString().Should().Be("Seer Fish");
        complaint.GetProperty("variantName").GetString().Should().Be("Medium");
        complaint.GetProperty("maxRefundPaise").GetInt64().Should().Be(expected);

        var staff = Staff();
        var detail = await ReadJsonAsync(await staff.GetAsync(new Uri($"/api/v1/admin/complaints/{complaintId}", UriKind.Relative), Ct));
        detail.GetProperty("productName").GetString().Should().Be("Seer Fish");
        detail.GetProperty("maxRefundPaise").GetInt64().Should().Be(expected);

        var list = await ReadJsonAsync(await staff.GetAsync(new Uri($"/api/v1/admin/complaints?fulfillmentLocationId={CheckoutWorld.LocationId}&status=OPEN&pageSize=100", UriKind.Relative), Ct));
        var row = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == complaintId);
        row.GetProperty("maxRefundPaise").GetInt64().Should().Be(expected);
        row.GetProperty("preparationName").GetString().Should().Be("Steaks");

        var over = await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/complaints/{complaintId}/decide", Quoted(detail),
            new { status = "APPROVED", resolution = "REFUND", refundAmountPaise = expected + 1 });
        CodeOf(await ReadJsonAsync(over)).Should().Be("REFUND_AMOUNT_INVALID");
    }

    [Fact]
    public async Task ORD_R66_photos_of_complaints_closed_past_retention_are_purged_and_open_ones_kept()
    {
        var staff = Staff();
        var (closedOrder, closedFish) = await DeliveredOrderAsync();
        var closed = (await RaiseAsync(closedOrder, closedFish.Id)).GetProperty("id").GetGuid();
        (await UploadAsync(Customer, $"/api/v1/orders/{closedOrder}/complaints/{closed}/photos", Png(400, 400))).StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await ReadJsonAsync(await staff.GetAsync(new Uri($"/api/v1/admin/complaints/{closed}", UriKind.Relative), Ct));
        (await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/complaints/{closed}/decide", Quoted(detail), new { status = "REJECTED" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var (openOrder, openFish) = await DeliveredOrderAsync();
        var open = (await RaiseAsync(openOrder, openFish.Id)).GetProperty("id").GetGuid();
        (await UploadAsync(Customer, $"/api/v1/orders/{openOrder}/complaints/{open}/photos", Png(400, 400))).StatusCode.Should().Be(HttpStatusCode.Created);
        var later = new TestClock(Factory.Clock.UtcNow.AddDays(31));

        await RunPurgeAsync(later, retentionDays: null);
        (await PhotoCountAsync(closed)).Should().Be(1, "no retention period is configured, so nothing is deleted");
        await RunPurgeAsync(new TestClock(Factory.Clock.UtcNow.AddDays(29)), retentionDays: 30);
        (await PhotoCountAsync(closed)).Should().Be(1, "the complaint closed less than 30 days ago");

        await RunPurgeAsync(later, retentionDays: 30);

        (await PhotoCountAsync(closed)).Should().Be(0);
        var folder = Path.Combine(Factory.StorageRoot, "complaints", closed.ToString());
        (Directory.Exists(folder) ? Directory.GetFiles(folder) : []).Should().BeEmpty("the file goes with the row");
        (await PhotoCountAsync(open)).Should().Be(1, "an OPEN complaint keeps its photos");
        var after = await ReadJsonAsync(await staff.GetAsync(new Uri($"/api/v1/admin/complaints/{closed}", UriKind.Relative), Ct));
        after.GetProperty("photos").GetArrayLength().Should().Be(0);
    }

    private Task<int> RunPurgeAsync(TestClock clock, int? retentionDays)
        => InScopeAsync(async (services, db) =>
        {
            var job = new ComplaintPhotoPurgeJob(db, services.GetRequiredService<IFileStorage>(), clock, new OrderingOptions { ComplaintPhotoRetentionDays = retentionDays }, NullLogger<ComplaintPhotoPurgeJob>.Instance);
            await job.ExecuteAsync(Ct);
            return 0;
        });

    private Task<int> PhotoCountAsync(Guid complaintId)
        => InScopeAsync((_, db) => db.Set<ComplaintPhoto>().CountAsync(p => p.ComplaintId == complaintId, Ct));

    [Fact]
    public void W12_photo_upload_is_rate_limited_per_customer()
    {
        var endpoint = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/api/v1/orders/{orderId:guid}/complaints/{complaintId:guid}/photos"
                && e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains("POST") == true);

        var limit = endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();
        limit.Should().NotBeNull();
        limit!.PolicyName.Should().Be(RateLimitPolicies.Upload);
    }

    [Fact]
    public async Task W12_customer_adds_photos_staff_and_customer_read_them_and_nobody_else_can()
    {
        var (orderId, fish) = await DeliveredOrderAsync();
        var complaintId = (await RaiseAsync(orderId, fish.Id)).GetProperty("id").GetGuid();
        var photosUrl = $"/api/v1/orders/{orderId}/complaints/{complaintId}/photos";

        var upload = await UploadAsync(Customer, photosUrl, Png(1600, 1200));

        upload.StatusCode.Should().Be(HttpStatusCode.Created, await upload.Content.ReadAsStringAsync(Ct));
        var body = await ReadJsonAsync(upload);
        var photo = body.GetProperty("photos").EnumerateArray().Should().ContainSingle().Subject;
        var photoId = photo.GetProperty("id").GetGuid();
        photo.GetProperty("width").GetInt32().Should().Be(1280);
        photo.GetProperty("height").GetInt32().Should().Be(960);
        photo.GetProperty("url").GetString().Should().Be($"{photosUrl}/{photoId}");
        body.GetProperty("attachmentUrls").EnumerateArray().Select(u => u.GetString()).Should().Equal($"{photosUrl}/{photoId}");

        var mine = await Customer.GetAsync(new Uri($"{photosUrl}/{photoId}", UriKind.Relative), Ct);
        mine.StatusCode.Should().Be(HttpStatusCode.OK);
        mine.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        (await mine.Content.ReadAsByteArrayAsync(Ct)).AsSpan(8, 4).SequenceEqual("WEBP"u8).Should().BeTrue();

        var staff = Staff();
        var detail = await ReadJsonAsync(await staff.GetAsync(new Uri($"/api/v1/admin/complaints/{complaintId}", UriKind.Relative), Ct));
        var staffUrl = detail.GetProperty("photos")[0].GetProperty("url").GetString();
        staffUrl.Should().Be($"/api/v1/admin/complaints/{complaintId}/photos/{photoId}");
        (await staff.GetAsync(new Uri(staffUrl!, UriKind.Relative), Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var other = ClientFor(f => f.CreateCustomerClient());
        (await other.GetAsync(new Uri($"{photosUrl}/{photoId}", UriKind.Relative), Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UploadAsync(other, photosUrl, Png(800, 800))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var outsider = ClientFor(f => f.CreateStaffClient(["complaints.manage"], [Guid.NewGuid()]));
        (await outsider.GetAsync(new Uri(staffUrl!, UriKind.Relative), Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Stored privately: no file extension, so the public /media route never serves it (SEC-20).
        var stored = Directory.GetFiles(Path.Combine(Factory.StorageRoot, "complaints", complaintId.ToString()));
        stored.Should().ContainSingle().Which.Should().EndWith(photoId.ToString());
    }

    [Fact]
    public async Task W12_bad_images_the_photo_limit_and_a_decided_complaint_are_refused()
    {
        var (orderId, fish) = await DeliveredOrderAsync();
        var complaintId = (await RaiseAsync(orderId, fish.Id)).GetProperty("id").GetGuid();
        var photosUrl = $"/api/v1/orders/{orderId}/complaints/{complaintId}/photos";

        await ExpectRefusalAsync(await UploadAsync(Customer, photosUrl, Png(200, 200)), "IMAGE_INVALID", "file");
        await ExpectRefusalAsync(await UploadAsync(Customer, photosUrl, "not an image at all"u8.ToArray()), "IMAGE_INVALID", "file");
        await ExpectRefusalAsync(await UploadAsync(Customer, photosUrl, null), "IMAGE_INVALID", "file");

        for (var i = 0; i < 4; i++)
        {
            (await UploadAsync(Customer, photosUrl, Png(400, 400))).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        await ExpectRefusalAsync(await UploadAsync(Customer, photosUrl, Png(400, 400)), "COMPLAINT_PHOTO_LIMIT_REACHED", "file");

        var staff = Staff();
        var detail = await ReadJsonAsync(await staff.GetAsync(new Uri($"/api/v1/admin/complaints/{complaintId}", UriKind.Relative), Ct));
        detail.GetProperty("photos").GetArrayLength().Should().Be(4);
        var reject = await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/complaints/{complaintId}/decide", Quoted(detail), new { status = "REJECTED" });
        reject.StatusCode.Should().Be(HttpStatusCode.OK, await reject.Content.ReadAsStringAsync(Ct));

        var (secondOrder, secondFish) = await DeliveredOrderAsync();
        var secondComplaint = (await RaiseAsync(secondOrder, secondFish.Id)).GetProperty("id").GetGuid();
        var decide = await ReadJsonAsync(await staff.GetAsync(new Uri($"/api/v1/admin/complaints/{secondComplaint}", UriKind.Relative), Ct));
        await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/complaints/{secondComplaint}/decide", Quoted(decide), new { status = "REJECTED" });
        var afterDecision = await UploadAsync(Customer, $"/api/v1/orders/{secondOrder}/complaints/{secondComplaint}/photos", Png(400, 400));
        afterDecision.StatusCode.Should().Be(HttpStatusCode.Conflict);
        CodeOf(await ReadJsonAsync(afterDecision)).Should().Be("INVALID_STATUS_TRANSITION");
    }

    [Fact]
    public async Task Open_complaint_blocks_account_deletion_until_it_is_decided()
    {
        var (orderId, fish) = await DeliveredOrderAsync();
        (await DeletionBlockersAsync()).Should().BeEmpty();
        var complaintId = (await RaiseAsync(orderId, fish.Id)).GetProperty("id").GetGuid();

        (await DeletionBlockersAsync()).Should().Equal("OPEN_COMPLAINT");

        var staff = Staff();
        var detail = await ReadJsonAsync(await staff.GetAsync(new Uri($"/api/v1/admin/complaints/{complaintId}", UriKind.Relative), Ct));
        (await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/complaints/{complaintId}/decide", Quoted(detail), new { status = "REJECTED" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await DeletionBlockersAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task G16_order_numbers_are_read_in_one_batch_and_unknown_ids_are_absent()
    {
        var orderId = await PlaceOrderAsync();
        var number = (await LoadOrderAsync(orderId))!.OrderNumber;

        var numbers = await InScopeAsync((s, _) => s.GetRequiredService<FreshOrbit.Ordering.Public.IOrderingApi>().GetOrderNumbersAsync([orderId, Guid.NewGuid()], Ct));

        numbers.Should().ContainSingle().Which.Should().Be(new KeyValuePair<Guid, string>(orderId, number));
    }

    private Task<IReadOnlyList<string>> DeletionBlockersAsync()
        => InScopeAsync((s, _) => s.GetRequiredService<FreshOrbit.Ordering.Infrastructure.Public.OrderingCustomerDeletionCheck>().GetBlockingItemsAsync(CustomerId, Ct));

    private static string Quoted(JsonElement resource) => '"' + resource.GetProperty("version").GetString() + '"';

    private static async Task ExpectRefusalAsync(HttpResponseMessage response, string code, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync(Ct));
        var problem = await ReadJsonAsync(response);
        CodeOf(problem).Should().Be(code);
        problem.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString().Should().Be(code);
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string url, byte[]? bytes)
    {
        using var form = new MultipartFormDataContent();
        if (bytes is not null)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "photo.png");
        }
        else
        {
            form.Add(new StringContent("none"), "note");
        }

        return await client.PostAsync(new Uri(url, UriKind.Relative), form, Ct);
    }

    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(200, 80, 40));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private async Task<JsonElement> RaiseAsync(Guid orderId, Guid itemId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/orders/{orderId}/complaints")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { orderItemId = itemId, kind = "QUALITY", description = "The fish smelled off when opened." }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await Customer.SendAsync(request, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return await ReadJsonAsync(response);
    }

    /// <summary>Places, packs, dispatches and delivers a COD order; returns it and its fish line (with final totals).</summary>
    private async Task<(Guid OrderId, OrderItem Fish)> DeliveredOrderAsync()
    {
        Factory.Clock.Set(Start);
        var orderId = await PlaceOrderAsync();
        var staff = Staff();
        await StartPreparingAsync(staff, orderId);
        var order = await LoadOrderAsync(orderId);
        var pack = await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/pack", await ETagOfAsync(staff, orderId), new
        {
            lines = new[]
            {
                new { orderItemId = order!.Items.Single(i => i.LineNo == 1).Id, packedQuantity = new { value = 500, unit = "GRAM" } },
                new { orderItemId = order.Items.Single(i => i.LineNo == 2).Id, packedQuantity = new { value = 2, unit = "PIECE" } },
            },
            coldBoxUsed = true,
        });
        pack.StatusCode.Should().Be(HttpStatusCode.OK, await pack.Content.ReadAsStringAsync(Ct));
        await AsHandlerAsync(s => s.GetRequiredService<OrderEventService>().OnShipmentDispatchedAsync(orderId, Guid.NewGuid(), null, Start.AddHours(4), Ct));
        await AsHandlerAsync(s => s.GetRequiredService<OrderEventService>().OnShipmentDeliveredAsync(orderId, Guid.NewGuid(), Start.AddHours(5), Ct));
        Factory.Clock.Set(Start.AddHours(6));
        var delivered = await LoadOrderAsync(orderId);
        delivered!.Status.Should().Be(OrderStatus.Delivered);
        return (orderId, delivered.Items.Single(i => i.LineNo == 1));
    }

    private static async Task StartPreparingAsync(HttpClient staff, Guid orderId)
    {
        var response = await SendWithVersionAsync(staff, HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/start-preparing", await ETagOfAsync(staff, orderId));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<Dictionary<Guid, string>> ListIdsAsync(HttpClient staff, string url)
    {
        var response = await staff.GetAsync(new Uri(url + "&pageSize=100", UriKind.Relative), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("id").GetGuid(), i => i.GetProperty("version").GetString()!);
    }
}
