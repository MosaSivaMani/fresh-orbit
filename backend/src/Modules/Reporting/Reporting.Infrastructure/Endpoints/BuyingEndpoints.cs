using System.Globalization;
using System.Text;
using FreshOrbit.Reporting.Domain;
using FreshOrbit.Reporting.Infrastructure.Buying;
using FreshOrbit.Reporting.Infrastructure.Reports;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace FreshOrbit.Reporting.Infrastructure.Endpoints;

/// <summary>
/// The morning buying list (REP-R14, permission <c>inventory.view</c>) and "today's prices" (REP-R15, permission
/// <c>pricing.manage</c>). Live read-only queries: no If-Match, no Idempotency-Key, not cached (REP-R13 otherwise).
/// </summary>
internal static class BuyingEndpoints
{
    public const string InventoryView = "inventory.view";
    public const string PricingManage = "pricing.manage";

    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private static readonly string[] CsvHeader =
    [
        "row_type", "product_name", "variant_name", "sku", "preparation_code", "preparation_name", "stock_unit", "yield_bp",
        "orders_count", "ordered_quantity", "ordered_net_grams", "needed_quantity", "estimated_raw_grams", "with_margin_quantity",
        "with_margin_raw_grams", "on_hand", "reserved_for_other_orders", "available_quantity", "shortfall_quantity", "shortfall_raw_grams",
    ];

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var reports = endpoints.MapGroup("/admin/reports");

        reports.MapGet("/buying-list", async (HttpContext http, BuyingReader reader, CancellationToken ct) =>
        {
            var request = http.Request;
            var errors = new List<FieldError>();
            var locationId = RequiredLocation(request, errors);
            var deliveryDate = ParseDate(request, "deliveryDate", errors);
            var margin = ParseInt(request, "safetyMarginBp", errors) ?? 0;
            var csv = ParseFormat(request, errors);
            if (errors.Count > 0)
            {
                throw new RefusalException(errors);
            }

            var list = await reader.BuyingListAsync(locationId, deliveryDate, margin, ct);
            NoStore(http.Response);
            if (!csv)
            {
                return Results.Ok(list);
            }

            await WriteCsvAsync(http.Response, list, ct);
            return Results.Empty;
        })
            .RequirePermission(InventoryView)
            .Produces<BuyingListResponse>(); // format=csv answers text/csv instead

        reports.MapGet("/todays-prices", async (HttpContext http, BuyingReader reader, CancellationToken ct) =>
        {
            var request = http.Request;
            var errors = new List<FieldError>();
            var locationId = RequiredLocation(request, errors);
            var categoryId = ParseGuid(request, "categoryId", errors);
            var target = ParseInt(request, "targetMarginBp", errors);
            var categoryTargets = ParseCategoryTargets(request, errors);
            if (errors.Count > 0)
            {
                throw new RefusalException(errors);
            }

            var prices = await reader.TodaysPricesAsync(locationId, categoryId, target, categoryTargets, ct);
            NoStore(http.Response);
            return Results.Ok(prices);
        })
            .RequirePermission(PricingManage)
            .Produces<TodaysPricesResponse>();
    }

    private static async Task WriteCsvAsync(HttpResponse response, BuyingListResponse list, CancellationToken ct)
    {
        response.ContentType = "text/csv; charset=utf-8";
        var date = list.DeliveryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        response.Headers[HeaderNames.ContentDisposition] = $"attachment; filename=\"buying_list_{list.LocationCode}_{date}.csv\"";
        await using var writer = new StreamWriter(response.Body, Utf8WithBom, bufferSize: 16 * 1024, leaveOpen: true);
        await writer.WriteAsync(ReportCsv.Line(CsvHeader).AsMemory(), ct);
        foreach (var item in list.Items)
        {
            await writer.WriteAsync(ReportCsv.Line(
            [
                "VARIANT", Text(item.ProductName), Text(item.VariantName), Text(item.Sku), string.Empty, string.Empty, item.StockUnit, string.Empty,
                Number(item.OrdersCount), Number(item.OrderedQuantity), Number(item.OrderedNetGrams), Number(item.NeededQuantity),
                Number(item.EstimatedRawGrams), Number(item.WithMarginQuantity), Number(item.WithMarginRawGrams), Number(item.OnHand),
                Number(item.ReservedForOtherOrders), Number(item.AvailableQuantity), Number(item.ShortfallQuantity), Number(item.ShortfallRawGrams),
            ]).AsMemory(), ct);
            foreach (var preparation in item.Preparations)
            {
                await writer.WriteAsync(ReportCsv.Line(
                [
                    "PREPARATION", Text(item.ProductName), Text(item.VariantName), Text(item.Sku), Text(preparation.PreparationCode),
                    Text(preparation.PreparationName), item.StockUnit, Number(preparation.YieldBp), Number(preparation.OrdersCount),
                    Number(preparation.OrderedQuantity), Number(preparation.OrderedNetGrams), Number(preparation.NeededQuantity),
                    Number(preparation.EstimatedRawGrams), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                ]).AsMemory(), ct);
            }
        }

        await writer.WriteAsync(ReportCsv.Line(
        [
            "TOTAL", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Number(list.OrdersCount),
            string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Number(list.EstimatedRawGrams), string.Empty, string.Empty,
            string.Empty, string.Empty, Number(list.ShortfallRawGrams),
        ]).AsMemory(), ct);
        await writer.FlushAsync(ct);
    }

    private static string Text(string value) => ReportCsv.Neutralize(value);

    private static string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static void NoStore(HttpResponse response) => response.Headers[HeaderNames.CacheControl] = "no-store";

    private static Guid RequiredLocation(HttpRequest request, List<FieldError> errors)
    {
        var id = ParseGuid(request, "fulfillmentLocationId", errors);
        if (id is null && !errors.Exists(e => e.Field == "fulfillmentLocationId"))
        {
            errors.Add(new FieldError("fulfillmentLocationId", ReportingErrors.ValidationFailed, "Choose a fulfillment location."));
        }

        return id ?? Guid.Empty;
    }

    private static bool ParseFormat(HttpRequest request, List<FieldError> errors)
    {
        var format = request.Query["format"].ToString();
        if (format.Length > 0 && format is not ("json" or "csv"))
        {
            errors.Add(new FieldError("format", ReportingErrors.ValidationFailed, "Format must be json or csv."));
        }

        return format == "csv";
    }

    private static DateOnly? ParseDate(HttpRequest request, string name, List<FieldError> errors)
    {
        var raw = request.Query[name].ToString();
        if (raw.Length == 0)
        {
            return null;
        }

        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        errors.Add(new FieldError(name, ReportingErrors.ValidationFailed, $"{name} must be a date (yyyy-MM-dd)."));
        return null;
    }

    private static Guid? ParseGuid(HttpRequest request, string name, List<FieldError> errors)
    {
        var raw = request.Query[name].ToString();
        if (raw.Length == 0)
        {
            return null;
        }

        if (Guid.TryParse(raw, CultureInfo.InvariantCulture, out var id))
        {
            return id;
        }

        errors.Add(new FieldError(name, ReportingErrors.ValidationFailed, $"{name} must be a UUID."));
        return null;
    }

    private static int? ParseInt(HttpRequest request, string name, List<FieldError> errors)
    {
        var raw = request.Query[name].ToString();
        if (raw.Length == 0)
        {
            return null;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add(new FieldError(name, ReportingErrors.ValidationFailed, $"{name} must be a whole number of basis points."));
        return null;
    }

    /// <summary><c>categoryTargets=&lt;categoryId&gt;:&lt;bp&gt;,…</c>: targets the owner typed per category on the screen (D371).</summary>
    private static Dictionary<Guid, int> ParseCategoryTargets(HttpRequest request, List<FieldError> errors)
    {
        var targets = new Dictionary<Guid, int>();
        var raw = request.Query["categoryTargets"].ToString();
        if (raw.Length == 0)
        {
            return targets;
        }

        foreach (var pair in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split(':');
            if (parts.Length != 2
                || !Guid.TryParse(parts[0], CultureInfo.InvariantCulture, out var categoryId)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bp)
                || !targets.TryAdd(categoryId, bp))
            {
                errors.Add(new FieldError("categoryTargets", ReportingErrors.ValidationFailed, "Category targets must be distinct categoryId:bp pairs separated by commas."));
                return targets;
            }
        }

        return targets;
    }
}
