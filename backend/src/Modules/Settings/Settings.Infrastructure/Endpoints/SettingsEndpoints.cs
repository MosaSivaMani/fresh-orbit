using FreshOrbit.Settings.Application.BusinessProfiles;
using FreshOrbit.Settings.Application.Numbering;
using FreshOrbit.Settings.Application.OrderPolicies;
using FreshOrbit.Settings.Application.Shared;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.Errors;
using FreshOrbit.SharedKernel.Web.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Settings.Infrastructure.Endpoints;

/// <summary>Staff endpoints under <c>/api/v1/admin/settings</c> (permission <c>settings.manage</c>).</summary>
internal static class SettingsEndpoints
{
    public const string Permission = "settings.manage";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/settings").RequirePermission(Permission).WithTags("Settings");

        group.MapGet("/business-profile", async (IBusinessProfileService service, HttpResponse response, CancellationToken ct) =>
        {
            var profile = await service.GetAsync(ct);
            SetETag(response, profile.Version);
            return TypedResults.Ok(profile);
        });

        group.MapPut("/business-profile", async (SaveBusinessProfileRequest request, IBusinessProfileService service, HttpContext http, CancellationToken ct) =>
        {
            var profile = await service.SaveAsync(request, ReadCondition(http.Request), ct);
            SetETag(http.Response, profile.Version);
            return TypedResults.Ok(profile);
        }).WithValidation<SaveBusinessProfileRequest>();

        group.MapGet("/order-policies", async (IOrderPolicyService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(ct)));

        group.MapPut("/order-policies/{key}", async (string key, SaveOrderPolicyRequest request, IOrderPolicyService service, HttpContext http, CancellationToken ct) =>
        {
            Guid? cityId = string.Equals(key, "default", StringComparison.OrdinalIgnoreCase)
                ? null
                : Guid.TryParse(key, out var id) ? id : throw new BadHttpRequestException("The path must end with a city id or 'default'.");
            var policy = await service.SaveAsync(cityId, request, ReadCondition(http.Request), ct);
            SetETag(http.Response, policy.Version);
            return TypedResults.Ok(policy);
        });

        group.MapGet("/number-series", async (INumberSeriesService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(ct)));

        group.MapPut("/number-series/{code}/{period}", async (string code, string period, SaveNumberSeriesRequest request, INumberSeriesService service, HttpContext http, CancellationToken ct) =>
        {
            var series = await service.SaveAsync(code, period, request, ReadCondition(http.Request), ct);
            SetETag(http.Response, series.Version);
            return TypedResults.Ok(series);
        });
    }

    /// <summary><c>If-Match</c> (update) or <c>If-None-Match: *</c> (create); neither → 428.</summary>
    internal static WriteCondition ReadCondition(HttpRequest request)
    {
        var version = IfMatch.TryGetVersion(request);
        var createOnly = request.Headers.IfNoneMatch.ToString().Trim() == "*";
        if (version is null && !createOnly)
        {
            throw new VersionRequiredException();
        }

        return new WriteCondition(version, createOnly && version is null);
    }

    private static void SetETag(HttpResponse response, string version) => response.SetETag(uint.Parse(version, System.Globalization.CultureInfo.InvariantCulture));
}
