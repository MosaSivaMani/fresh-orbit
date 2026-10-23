using System.Globalization;
using System.Text;
using FreshOrbit.Reporting.Application;
using FreshOrbit.Reporting.Domain;
using FreshOrbit.Reporting.Infrastructure.Reports;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace FreshOrbit.Reporting.Infrastructure.Endpoints;

public static class ReportingPermissions
{
    public const string ReportsView = "reports.view";
}

/// <summary>Maps <see cref="LocationAccessDeniedException"/> to 403 (the shared handler maps domain refusals to 422).</summary>
public sealed class ReportingProblemFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(context);
        }
        catch (LocationAccessDeniedException exception)
        {
            var problem = FreshOrbitExceptionHandler.ToProblem(exception);
            return TypedResults.Problem(detail: problem.Detail, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden.", extensions: problem.Extensions);
        }
    }
}

/// <summary>Staff report endpoints (docs/api §4.20). Read-only: no If-Match, no Idempotency-Key (REP-R13).</summary>
internal static class ReportingEndpoints
{
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
    private static readonly string[] CohortKeys = ["cohortMonth", "monthOffset", "cohortSize", "activeCustomers", "retentionBp"];

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var reports = endpoints.MapGroup("/admin/reports")
            .AddEndpointFilter<ReportingProblemFilter>()
            .RequirePermission(ReportingPermissions.ReportsView);

        foreach (var report in ReportDefinitions.All)
        {
            reports.MapGet("/" + report.Key, (HttpContext http, ReportReader reader, CancellationToken ct) => ServeAsync(report, http, reader, ct));
        }

        reports.MapGet("/repeat-customers", async (HttpContext http, ReportReader reader, CancellationToken ct) =>
        {
            var query = ParseQuery(http.Request);
            var errors = new List<FieldError>();
            var cohortMonths = ParseInt(http.Request, "cohortMonths", 6, errors);
            if (errors.Count > 0)
            {
                throw new RefusalException(errors);
            }

            var response = await reader.RepeatCustomersAsync(query, cohortMonths, ct);
            SetCache(http.Response);
            if (query.Csv)
            {
                await WriteCohortCsvAsync(http.Response, response, ct);
                return Results.Empty;
            }

            return Results.Ok(response);
        });

        reports.MapGet("/dashboard", async (HttpContext http, ReportReader reader, CancellationToken ct) =>
        {
            var query = ParseQuery(http.Request);
            if (query.Csv)
            {
                throw new RefusalException([new FieldError("format", ReportingErrors.ValidationFailed, "The dashboard has no CSV export.")]);
            }

            var dashboard = await reader.DashboardAsync(query, ct);
            SetCache(http.Response);
            return Results.Ok(dashboard);
        });
    }

    private static async Task<IResult> ServeAsync(ReportDefinition report, HttpContext http, ReportReader reader, CancellationToken ct)
    {
        var query = ParseQuery(http.Request);
        SetCache(http.Response);
        if (!query.Csv)
        {
            return Results.Ok(await reader.ReadAsync(report, query, ct));
        }

        var export = await reader.ExportAsync(report, query, ct);
        var response = http.Response;
        var fileName = $"{report.View.Name}_{Iso(export.Prepared.From)}_{Iso(export.Prepared.To)}.csv";
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers[HeaderNames.ContentDisposition] = $"attachment; filename=\"{fileName}\"";
        if (export.Prepared.AsOf is { } asOf)
        {
            response.Headers["X-Report-As-Of"] = asOf.ToString("O", CultureInfo.InvariantCulture);
        }

        await using var writer = new StreamWriter(response.Body, Utf8WithBom, bufferSize: 16 * 1024, leaveOpen: true);
        await writer.WriteAsync(ReportCsv.Header(report).AsMemory(), ct);
        await foreach (var row in reader.StreamAsync(export, ct))
        {
            await writer.WriteAsync(ReportCsv.Row(report, row).AsMemory(), ct);
        }

        await writer.WriteAsync(ReportCsv.TotalRow(report, export.Totals).AsMemory(), ct);
        await writer.FlushAsync(ct);
        return Results.Empty;
    }

    private static async Task WriteCohortCsvAsync(HttpResponse response, ReportResponse report, CancellationToken ct)
    {
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers[HeaderNames.ContentDisposition] = $"attachment; filename=\"repeat_customers_{Iso(report.FromDate)}_{Iso(report.ToDate)}.csv\"";
        if (report.AsOf is { } asOf)
        {
            response.Headers["X-Report-As-Of"] = asOf.ToString("O", CultureInfo.InvariantCulture);
        }

        await using var writer = new StreamWriter(response.Body, Utf8WithBom, bufferSize: 16 * 1024, leaveOpen: true);
        await writer.WriteAsync("cohort_month,month_offset,cohort_size,active_customers,retention_bp\r\n".AsMemory(), ct);
        foreach (var item in report.Items)
        {
            var line = string.Join(',', CohortKeys.Select(key => item[key] is DateOnly d ? Iso(d) : Convert.ToString(item[key], CultureInfo.InvariantCulture)));
            await writer.WriteAsync((line + "\r\n").AsMemory(), ct);
        }

        var totals = report.Totals;
        await writer.WriteAsync(string.Create(CultureInfo.InvariantCulture, $"TOTAL,,{totals["activeCustomers"]},{totals["repeatCustomers"]},{totals["repeatRateBp"]}\r\n").AsMemory(), ct);
        await writer.FlushAsync(ct);
    }

    internal static ReportQuery ParseQuery(HttpRequest request)
    {
        var errors = new List<FieldError>();
        var fromDate = ParseDate(request, "fromDate", errors);
        var toDate = ParseDate(request, "toDate", errors);
        var cityId = ParseGuid(request, "cityId", errors);
        var locationId = ParseGuid(request, "fulfillmentLocationId", errors);
        var page = ParseInt(request, "page", 1, errors);
        var pageSize = ParseInt(request, "pageSize", 20, errors);
        var format = request.Query["format"].ToString();
        if (format.Length > 0 && format is not ("json" or "csv"))
        {
            errors.Add(new FieldError("format", ReportingErrors.ValidationFailed, "Format must be json or csv."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        var extra = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.Ordinal);
        return new ReportQuery(fromDate, toDate, cityId, locationId, page, pageSize, request.Query["sort"].ToString(), format == "csv", extra);
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

    private static int ParseInt(HttpRequest request, string name, int fallback, List<FieldError> errors)
    {
        var raw = request.Query[name].ToString();
        if (raw.Length == 0)
        {
            return fallback;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add(new FieldError(name, ReportingErrors.ValidationFailed, $"{name} must be a whole number."));
        return fallback;
    }

    private static void SetCache(HttpResponse response) => response.Headers[HeaderNames.CacheControl] = "private, max-age=60";

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
