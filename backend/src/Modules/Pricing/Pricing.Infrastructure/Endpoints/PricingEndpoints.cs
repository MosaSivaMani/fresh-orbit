using System.Globalization;
using FreshOrbit.Pricing.Application.Common;
using FreshOrbit.Pricing.Application.Contracts;
using FreshOrbit.Pricing.Application.Schedules;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.Idempotency;
using FreshOrbit.SharedKernel.Web.Paging;
using FreshOrbit.SharedKernel.Web.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace FreshOrbit.Pricing.Infrastructure.Endpoints;

/// <summary>docs/api/README.md §4.12 (admin pricing).</summary>
internal static class PricingEndpoints
{
    public const string ManagePermission = "pricing.manage";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/pricing").AddEndpointFilter<PricingProblemFilter>().WithTags("Admin pricing");

        group.MapGet("/prices", async (
                [AsParameters] PagedRequest paging,
                Guid? fulfillmentLocationId,
                Guid? variantId,
                string? q,
                bool? missingOnly,
                PricingService service,
                CancellationToken ct) =>
            {
                var (items, total) = await service.ListPricesAsync(fulfillmentLocationId, variantId, q, missingOnly ?? false, paging.Skip, paging.PageSize, ct);
                return TypedResults.Ok(new PagedResponse<PriceListItem>(items, paging.Page, paging.PageSize, total));
            })
            .WithValidation<PagedRequest>()
            .RequirePermission(ManagePermission);

        group.MapGet("/locations/{locationId:guid}/variants/{variantId:guid}/price", async (Guid locationId, Guid variantId, PricingService service, HttpResponse response, CancellationToken ct) =>
                Respond(response, await service.GetPriceAsync(locationId, variantId, ct), r => r.Version))
            .RequirePermission(ManagePermission);

        group.MapPut("/locations/{locationId:guid}/variants/{variantId:guid}/price", async (Guid locationId, Guid variantId, SetPriceRequest request, PricingService service, HttpContext http, CancellationToken ct) =>
                Respond(http.Response, await service.SetPriceAsync(locationId, variantId, Precondition(http.Request), request, ct), r => r.Version))
            .RequirePermission(ManagePermission);

        group.MapDelete("/locations/{locationId:guid}/variants/{variantId:guid}/price", async (Guid locationId, Guid variantId, PricingService service, HttpContext http, CancellationToken ct) =>
                Respond(http.Response, await service.EndPriceAsync(locationId, variantId, Precondition(http.Request), ct), r => r.Version))
            .RequirePermission(ManagePermission);

        group.MapGet("/locations/{locationId:guid}/variants/{variantId:guid}/preparations/{code}/charge", async (Guid locationId, Guid variantId, string code, PricingService service, HttpResponse response, CancellationToken ct) =>
                Respond(response, await service.GetChargeAsync(locationId, variantId, code, ct), r => r.Version))
            .RequirePermission(ManagePermission);

        group.MapPut("/locations/{locationId:guid}/variants/{variantId:guid}/preparations/{code}/charge", async (Guid locationId, Guid variantId, string code, SetChargeRequest request, PricingService service, HttpContext http, CancellationToken ct) =>
                Respond(http.Response, await service.SetChargeAsync(locationId, variantId, code, Precondition(http.Request), request, ct), r => r.Version))
            .RequirePermission(ManagePermission);

        group.MapGet("/tax-rules", async (PricingService service, CancellationToken ct) => TypedResults.Ok(await service.ListTaxRulesAsync(ct)))
            .RequirePermission(ManagePermission);

        group.MapPost("/tax-rules", async (ScheduleTaxRuleRequest request, PricingService service, HttpResponse response, CancellationToken ct) =>
                Respond(response, await service.ScheduleTaxRuleAsync(request, ct), r => r.Version))
            .RequirePermission(ManagePermission);

        group.MapPost("/prices/bulk", async (bool? dryRun, DateTimeOffset? validFrom, bool? replaceFuture, HttpRequest request, BulkPriceService service, CancellationToken ct) =>
            {
                var body = await BulkPriceReader.ReadAsync(request, validFrom, replaceFuture, ct);
                return TypedResults.Ok(await service.ApplyAsync(body, dryRun ?? false, ct));
            })
            .WithIdempotency(required: true)
            .RequirePermission(ManagePermission);
    }

    /// <summary><c>If-Match</c> version, or <c>If-None-Match: *</c> for a schedule that has no period yet.</summary>
    private static SchedulePrecondition Precondition(HttpRequest request)
        => new(IfMatch.TryGetVersion(request), request.Headers[HeaderNames.IfNoneMatch].ToString().Trim() == "*");

    private static Ok<T> Respond<T>(HttpResponse response, T body, Func<T, string?> version)
    {
        if (version(body) is { } value)
        {
            response.SetETag(uint.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture));
        }

        return TypedResults.Ok(body);
    }
}

/// <summary>
/// Problem Details the shared handler cannot produce: 403 LOCATION_ACCESS_DENIED, 428 VERSION_REQUIRED from the
/// schedule check, and an EXCLUDE violation (a concurrent writer on the same key, PRC-R16) as 409 CONCURRENT_SAVE.
/// </summary>
public sealed class PricingProblemFilter : IEndpointFilter
{
    private const string ExclusionViolation = "23P01";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(context);
        }
        catch (PricingLocationAccessDeniedException exception)
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden.", exception.Code, exception.Message, null);
        }
        catch (PricingVersionRequiredException exception)
        {
            return Problem(StatusCodes.Status428PreconditionRequired, "Version required.", exception.Code, exception.Message, "If-Match");
        }
        catch (Exception exception) when (IsExclusionViolation(exception))
        {
            return Problem(StatusCodes.Status409Conflict, "Concurrent save.", ConcurrentSaveException.DefaultCode, "Another change to these periods was saved at the same time; reload and retry.", null);
        }
    }

    internal static bool IsExclusionViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is Npgsql.PostgresException { SqlState: ExclusionViolation })
            {
                return true;
            }
        }

        return false;
    }

    private static ProblemHttpResult Problem(int status, string title, string code, string detail, string? field)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (field is not null)
        {
            extensions["errors"] = new Dictionary<string, SharedKernel.Web.Errors.ProblemError[]> { [field] = [new(code, detail)] };
        }

        return TypedResults.Problem(detail: detail, statusCode: status, title: title, extensions: extensions);
    }
}
