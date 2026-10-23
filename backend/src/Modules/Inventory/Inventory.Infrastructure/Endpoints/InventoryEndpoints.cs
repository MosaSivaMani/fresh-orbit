using System.Globalization;
using FreshOrbit.Inventory.Application.Stock;
using FreshOrbit.Inventory.Domain.Shared;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Infrastructure.Reads;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.Idempotency;
using FreshOrbit.SharedKernel.Web.Paging;
using FreshOrbit.SharedKernel.Web.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Inventory.Infrastructure.Endpoints;

/// <summary>Permission codes of docs/api §3 used by Inventory.</summary>
public static class InventoryPermissions
{
    public const string View = "inventory.view";
    public const string Receive = "inventory.receive";
    public const string Adjust = "inventory.adjust";
}

/// <summary>Admin inventory endpoints (docs/api §4.14) under <c>/api/v1/admin/inventory</c>.</summary>
internal static class InventoryEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/inventory").WithTags("Admin inventory");

        admin.MapGet("/stock", async (Guid? fulfillmentLocationId, Guid? variantId, bool? lowStockOnly, int? page, int? pageSize, InventoryReads reads, CancellationToken ct)
                => TypedResults.Ok(await reads.ListStockAsync(new StockListQuery(fulfillmentLocationId, variantId, lowStockOnly ?? false), Paging(page, pageSize), ct)))
            .RequirePermission(InventoryPermissions.View);

        admin.MapGet("/stock/{stockItemId:guid}", async (Guid stockItemId, InventoryReads reads, HttpResponse response, CancellationToken ct) =>
            {
                var item = await reads.GetStockAsync(stockItemId, ct);
                response.SetETag(ParseVersion(item.Version));
                return TypedResults.Ok(item);
            })
            .RequirePermission(InventoryPermissions.View);

        admin.MapPut("/stock/{stockItemId:guid}/threshold", async (Guid stockItemId, SetThresholdRequest request, HttpRequest http, StockCommandService service, CancellationToken ct) =>
            {
                var item = await service.SetThresholdAsync(stockItemId, IfMatch.GetRequiredVersion(http), request, ct);
                http.HttpContext.Response.SetETag(ParseVersion(item.Version));
                return TypedResults.Ok(item);
            })
            .WithValidation<SetThresholdRequest>()
            .RequirePermission(InventoryPermissions.Adjust);

        admin.MapPost("/stock-items", async (CreateStockItemRequest request, StockCommandService service, HttpResponse response, CancellationToken ct) =>
            {
                var item = await service.CreateStockItemAsync(request, ct);
                response.SetETag(ParseVersion(item.Version));
                return TypedResults.Created($"/api/v1/admin/inventory/stock/{item.Id}", item);
            })
            .WithValidation<CreateStockItemRequest>()
            .RequirePermission(InventoryPermissions.Adjust);

        admin.MapPost("/receipts", async (ReceiveStockRequest request, StockCommandService service, HttpResponse response, CancellationToken ct) =>
            {
                var receipt = await service.ReceiveAsync(request, ct);
                response.SetETag(ParseVersion(receipt.StockItem.Version));
                return TypedResults.Created($"/api/v1/admin/inventory/stock/{receipt.StockItem.Id}", receipt);
            })
            .WithValidation<ReceiveStockRequest>()
            .WithIdempotency(required: true)
            .RequirePermission(InventoryPermissions.Receive);

        admin.MapGet("/batches", async (Guid? fulfillmentLocationId, Guid? variantId, Guid? stockItemId, string[]? status, DateTimeOffset? expiringBefore, int? page, int? pageSize, InventoryReads reads, CancellationToken ct)
                => TypedResults.Ok(await reads.ListBatchesAsync(new BatchListQuery(fulfillmentLocationId, variantId, stockItemId, ParseCodes<BatchStatus>(status, "status"), expiringBefore), Paging(page, pageSize), ct)))
            .RequirePermission(InventoryPermissions.View);

        admin.MapPost("/batches/{batchId:guid}/discard", async (Guid batchId, DiscardBatchRequest request, HttpRequest http, StockCommandService service, CancellationToken ct) =>
            {
                var change = await service.DiscardBatchAsync(batchId, IfMatch.GetRequiredVersion(http), request, ct);
                http.HttpContext.Response.SetETag(ParseVersion(change.StockItem.Version));
                return TypedResults.Ok(change);
            })
            .WithValidation<DiscardBatchRequest>()
            .RequirePermission(InventoryPermissions.Adjust);

        admin.MapPost("/waste", async (RecordWasteRequest request, StockCommandService service, HttpResponse response, CancellationToken ct) =>
            {
                var change = await service.RecordWasteAsync(request, ct);
                response.SetETag(ParseVersion(change.StockItem.Version));
                return TypedResults.Ok(change);
            })
            .WithValidation<RecordWasteRequest>()
            .WithIdempotency(required: true)
            .RequirePermission(InventoryPermissions.Adjust);

        admin.MapPost("/adjustments", async (AdjustStockRequest request, HttpRequest http, StockCommandService service, CancellationToken ct) =>
            {
                var change = await service.AdjustAsync(IfMatch.GetRequiredVersion(http), request, ct);
                http.HttpContext.Response.SetETag(ParseVersion(change.StockItem.Version));
                return TypedResults.Ok(change);
            })
            .WithValidation<AdjustStockRequest>()
            .RequirePermission(InventoryPermissions.Adjust);

        admin.MapPost("/counts", async (CountStockRequest request, HttpRequest http, StockCommandService service, CancellationToken ct) =>
            {
                var change = await service.CountAsync(IfMatch.GetRequiredVersion(http), request, ct);
                http.HttpContext.Response.SetETag(ParseVersion(change.StockItem.Version));
                return TypedResults.Ok(change);
            })
            .WithValidation<CountStockRequest>()
            .RequirePermission(InventoryPermissions.Adjust);

        admin.MapGet("/movements", async (Guid? stockItemId, Guid? fulfillmentLocationId, string[]? kind, DateOnly? fromDate, DateOnly? toDate, int? page, int? pageSize, InventoryReads reads, CancellationToken ct)
                => TypedResults.Ok(await reads.ListMovementsAsync(new MovementListQuery(stockItemId, fulfillmentLocationId, ParseCodes<MovementKind>(kind, "kind"), fromDate, toDate), Paging(page, pageSize), ct)))
            .RequirePermission(InventoryPermissions.View);

        admin.MapGet("/reservations", async (Guid? fulfillmentLocationId, Guid? orderId, string[]? status, int? page, int? pageSize, InventoryReads reads, CancellationToken ct)
                => TypedResults.Ok(await reads.ListReservationsAsync(new ReservationListQuery(fulfillmentLocationId, orderId, ParseCodes<ReservationStatus>(status, "status")), Paging(page, pageSize), ct)))
            .RequirePermission(InventoryPermissions.View);

        admin.MapGet("/cold-chain", async (Guid? fulfillmentLocationId, bool? outOfRangeOnly, int? page, int? pageSize, InventoryReads reads, CancellationToken ct)
                => TypedResults.Ok(await reads.ListColdChainAsync(new ColdChainListQuery(fulfillmentLocationId, outOfRangeOnly ?? false), Paging(page, pageSize), ct)))
            .RequirePermission(InventoryPermissions.View);

        admin.MapPost("/cold-chain", async (RecordTemperatureRequest request, StockCommandService service, CancellationToken ct) =>
            {
                var log = await service.RecordTemperatureAsync(request, ct);
                return TypedResults.Created($"/api/v1/admin/inventory/cold-chain/{log.Id}", log);
            })
            .WithValidation<RecordTemperatureRequest>()
            .RequirePermission(InventoryPermissions.Adjust);
    }

    private static PagedRequest Paging(int? page, int? pageSize) => new(page ?? 1, pageSize ?? PagedRequest.DefaultPageSize);

    private static uint ParseVersion(string version) => uint.Parse(version, NumberStyles.None, CultureInfo.InvariantCulture);

    private static List<TEnum> ParseCodes<TEnum>(string[]? codes, string field)
        where TEnum : struct, Enum
    {
        var values = new List<TEnum>();
        foreach (var code in codes ?? [])
        {
            if (!EnumCodes.TryToEnum<TEnum>(code, out var value))
            {
                throw new RefusalException(new FieldError(field, InventoryErrors.ValidationFailed, $"{field} must be one of {string.Join(", ", EnumCodes.AllCodes<TEnum>())}."));
            }

            values.Add(value);
        }

        return values;
    }
}
