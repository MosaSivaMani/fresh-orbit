using System.Globalization;
using FreshOrbit.Locations.Application.Cities;
using FreshOrbit.Locations.Application.Common;
using FreshOrbit.Locations.Application.FulfillmentLocations;
using FreshOrbit.Locations.Application.ServiceAreas;
using FreshOrbit.Locations.Application.Serviceability;
using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.Shared;
using FreshOrbit.Locations.Infrastructure.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.Paging;
using FreshOrbit.SharedKernel.Web.RateLimiting;
using FreshOrbit.SharedKernel.Web.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Locations.Infrastructure.Endpoints;

/// <summary>docs/api/README.md §4.3 (public) and §4.18 (admin).</summary>
internal static class LocationsEndpoints
{
    public const string ManagePermission = "locations.manage";
    public const string ActivatePermission = "cities.activate";
    private const string ProblemContentType = "application/problem+json";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        MapPublic(endpoints.MapGroup(string.Empty).AddEndpointFilter<LocationsProblemFilter>().WithTags("Locations"));
        var admin = endpoints.MapGroup("/admin").AddEndpointFilter<LocationsProblemFilter>().WithTags("Admin locations");
        MapCities(admin);
        MapFulfillmentLocations(admin);
        MapServiceAreas(admin);
        MapStaffLookups(admin);
    }

    private static void MapPublic(RouteGroupBuilder group)
    {
        group.MapGet("/serviceability", async (string? pincode, LocationsApi api, IEnumerable<INextSlotProvider> slotProviders, CancellationToken ct) =>
        {
            if (!Rules.IsPincode(pincode))
            {
                throw new RefusalException(new FieldError("pincode", LocationsErrors.ValidationFailed, "Enter a 6-digit pincode that does not start with 0."));
            }

            var served = await api.ResolveServedAsync(pincode, ct);
            if (served is null)
            {
                return TypedResults.Ok(new ServiceabilityResponse(pincode!, false, LocationsErrors.PincodeNotServiceable, null, null, null, null));
            }

            var provider = slotProviders.FirstOrDefault();
            var slot = provider is null ? null : await provider.GetNextOpenSlotAsync(served.FulfillmentLocationId, ct);
            return TypedResults.Ok(new ServiceabilityResponse(
                served.Pincode,
                true,
                null,
                new ServiceabilityCity(served.CityId, served.CityCode, served.CityName, served.TimeZone, served.StateCode, served.StateName),
                new ServiceabilityLocation(served.FulfillmentLocationId, served.FulfillmentLocationCode, served.FulfillmentLocationName, served.HasColdStorage),
                new ServiceabilityArea(served.ServiceAreaId, served.ServiceAreaName, served.DeliveryFeePaise, served.FreeDeliveryAbovePaise, served.MinOrderPaise),
                slot is null ? null : new ServiceabilitySlot(slot.SlotId, slot.StartAt, slot.EndAt)
                {
                    Date = Rules.Today(slot.StartAt, served.TimeZone),
                    CutoffAt = slot.CutoffAt,
                }));
        })
            .Produces<ServiceabilityResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireRateLimiting(RateLimitPolicies.Public);

        group.MapGet("/cities", async (ILocationsReads reads, CancellationToken ct) => TypedResults.Ok(await reads.ListActiveCitiesAsync(ct)))
            .RequireRateLimiting(RateLimitPolicies.Public);
    }

    private static void MapCities(RouteGroupBuilder admin)
    {
        admin.MapGet("/cities", async ([AsParameters] PagedRequest paging, [FromQuery(Name = "status")] string[]? status, ILocationsReads reads, CancellationToken ct) =>
            {
                var (items, total) = await reads.ListCitiesAsync(CityStatuses(status), paging.Skip, paging.PageSize, ct);
                return TypedResults.Ok(new PagedResponse<CityResponse>(items, paging.Page, paging.PageSize, total));
            })
            .WithValidation<PagedRequest>()
            .RequirePermission(ManagePermission);

        admin.MapPost("/cities", async (CreateCityRequest request, CityService service, HttpResponse response, CancellationToken ct) =>
            {
                var city = await service.CreateAsync(request, ct);
                SetETag(response, city.Version);
                return TypedResults.Created($"/api/v1/admin/cities/{city.Id}", city);
            })
            .RequirePermission(ManagePermission);

        admin.MapGet("/cities/{cityId:guid}", async (Guid cityId, ILocationsReads reads, CityService service, HttpResponse response, CancellationToken ct) =>
            {
                var city = await reads.GetCityAsync(cityId, ct) ?? throw new NotFoundException(LocationsErrors.CityNotFound, "No city has this id.");
                var readiness = await service.GetReadinessAsync(cityId, ct);
                SetETag(response, city.Version);
                return TypedResults.Ok(new CityDetailResponse(city.Id, city.Code, city.Name, city.StateCode, city.TimeZone, city.Status, city.ActivatedAt, city.ActivatedBy, city.Version, readiness)
                {
                    StateName = city.StateName,
                });
            })
            .RequirePermission(ManagePermission);

        admin.MapPut("/cities/{cityId:guid}", async (Guid cityId, UpdateCityRequest request, CityService service, HttpContext http, CancellationToken ct) =>
                Respond(http, await service.UpdateAsync(cityId, IfMatch.GetRequiredVersion(http.Request), request, ct), c => c.Version))
            .RequirePermission(ManagePermission);

        admin.MapGet("/cities/{cityId:guid}/readiness", async (Guid cityId, CityService service, CancellationToken ct) => TypedResults.Ok(await service.GetReadinessAsync(cityId, ct)))
            .RequirePermission(ManagePermission);

        admin.MapPost("/cities/{cityId:guid}/activate", async (Guid cityId, CityService service, HttpContext http, CancellationToken ct) =>
                Respond(http, await service.ActivateAsync(cityId, IfMatch.GetRequiredVersion(http.Request), ct), c => c.Version))
            .RequirePermission(ActivatePermission);

        admin.MapPost("/cities/{cityId:guid}/pause", async (Guid cityId, CityService service, HttpContext http, CancellationToken ct) =>
                Respond(http, await service.PauseAsync(cityId, IfMatch.GetRequiredVersion(http.Request), ct), c => c.Version))
            .RequirePermission(ActivatePermission);

        admin.MapPost("/cities/{cityId:guid}/retire", async (Guid cityId, CityService service, HttpContext http, CancellationToken ct) =>
                Respond(http, await service.RetireAsync(cityId, IfMatch.GetRequiredVersion(http.Request), ct), c => c.Version))
            .Produces<CityRetirementBlockedProblem>(StatusCodes.Status422UnprocessableEntity, ProblemContentType)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequirePermission(ActivatePermission);
    }

    private static void MapFulfillmentLocations(RouteGroupBuilder admin)
    {
        admin.MapGet("/fulfillment-locations", async ([AsParameters] PagedRequest paging, Guid? cityId, ILocationsReads reads, ICurrentActor actor, CancellationToken ct) =>
            {
                var (items, total) = await reads.ListLocationsAsync(cityId, actor.IsAllLocations ? null : actor.LocationIds, paging.Skip, paging.PageSize, ct);
                return TypedResults.Ok(new PagedResponse<FulfillmentLocationResponse>(items, paging.Page, paging.PageSize, total));
            })
            .WithValidation<PagedRequest>()
            .RequirePermission(ManagePermission);

        admin.MapPost("/fulfillment-locations", async (CreateFulfillmentLocationRequest request, FulfillmentLocationService service, HttpResponse response, CancellationToken ct) =>
            {
                var location = await service.CreateAsync(request, ct);
                SetETag(response, location.Version);
                return TypedResults.Created($"/api/v1/admin/fulfillment-locations/{location.Id}", location);
            })
            .RequirePermission(ManagePermission);

        admin.MapGet("/fulfillment-locations/{id:guid}", async (Guid id, ILocationsReads reads, ICurrentActor actor, HttpResponse response, CancellationToken ct) =>
            {
                LocationAccess.Ensure(actor, id);
                var location = await reads.GetLocationAsync(id, ct) ?? throw LocationNotFound();
                SetETag(response, location.Version);
                return TypedResults.Ok(location);
            })
            .RequirePermission(ManagePermission);

        admin.MapPut("/fulfillment-locations/{id:guid}", async (Guid id, UpdateFulfillmentLocationRequest request, FulfillmentLocationService service, HttpContext http, CancellationToken ct) =>
                Respond(http, await service.UpdateAsync(id, IfMatch.GetRequiredVersion(http.Request), request, ct), l => l.Version))
            .RequirePermission(ManagePermission);

        admin.MapPut("/fulfillment-locations/{id:guid}/operating-hours", async (Guid id, SetOperatingHoursRequest request, FulfillmentLocationService service, HttpContext http, CancellationToken ct) =>
                Respond(http, await service.SetOperatingHoursAsync(id, IfMatch.GetRequiredVersion(http.Request), request, ct), l => l.Version))
            .RequirePermission(ManagePermission);

        admin.MapGet("/fulfillment-locations/{id:guid}/closures", async (Guid id, DateOnly? fromDate, ILocationsReads reads, ICurrentActor actor, CancellationToken ct) =>
            {
                LocationAccess.Ensure(actor, id);
                _ = await reads.GetLocationAsync(id, ct) ?? throw LocationNotFound();
                return TypedResults.Ok(await reads.ListClosuresAsync(id, fromDate, ct));
            })
            .RequirePermission(ManagePermission);

        admin.MapPost("/fulfillment-locations/{id:guid}/closures", async (Guid id, AddClosureRequest request, FulfillmentLocationService service, CancellationToken ct) =>
            {
                var closure = await service.AddClosureAsync(id, request, ct);
                return TypedResults.Created($"/api/v1/admin/fulfillment-locations/{id}/closures/{closure.Id}", closure);
            })
            .RequirePermission(ManagePermission);

        admin.MapDelete("/fulfillment-locations/{id:guid}/closures/{closureId:guid}", async (Guid id, Guid closureId, FulfillmentLocationService service, CancellationToken ct) =>
            {
                await service.RemoveClosureAsync(id, closureId, ct);
                return TypedResults.NoContent();
            })
            .RequirePermission(ManagePermission);
    }

    private static void MapServiceAreas(RouteGroupBuilder admin)
    {
        admin.MapGet("/service-areas", async ([AsParameters] PagedRequest paging, Guid? cityId, Guid? fulfillmentLocationId, ILocationsReads reads, ICurrentActor actor, CancellationToken ct) =>
            {
                var (items, total) = await reads.ListServiceAreasAsync(cityId, fulfillmentLocationId, actor.IsAllLocations ? null : actor.LocationIds, paging.Skip, paging.PageSize, ct);
                return TypedResults.Ok(new PagedResponse<ServiceAreaResponse>(items, paging.Page, paging.PageSize, total));
            })
            .WithValidation<PagedRequest>()
            .RequirePermission(ManagePermission);

        admin.MapPost("/service-areas", async (CreateServiceAreaRequest request, ServiceAreaService service, HttpResponse response, CancellationToken ct) =>
            {
                var area = await service.CreateAsync(request, ct);
                SetETag(response, area.Version);
                return TypedResults.Created($"/api/v1/admin/service-areas/{area.Id}", area);
            })
            .RequirePermission(ManagePermission);

        admin.MapGet("/service-areas/{id:guid}", async (Guid id, ILocationsReads reads, ICurrentActor actor, HttpResponse response, CancellationToken ct) =>
            {
                var area = await reads.GetServiceAreaAsync(id, ct) ?? throw AreaNotFound();
                LocationAccess.Ensure(actor, area.FulfillmentLocationId);
                SetETag(response, area.Version);
                return TypedResults.Ok(area);
            })
            .RequirePermission(ManagePermission);

        admin.MapPut("/service-areas/{id:guid}", async (Guid id, UpdateServiceAreaRequest request, ServiceAreaService service, HttpContext http, CancellationToken ct) =>
                Respond(http, await service.UpdateAsync(id, IfMatch.GetRequiredVersion(http.Request), request, ct), a => a.Version))
            .RequirePermission(ManagePermission);

        admin.MapGet("/service-areas/{id:guid}/pincodes", async (Guid id, bool? includeRemoved, ILocationsReads reads, ICurrentActor actor, CancellationToken ct) =>
            {
                var area = await reads.GetServiceAreaAsync(id, ct) ?? throw AreaNotFound();
                LocationAccess.Ensure(actor, area.FulfillmentLocationId);
                return TypedResults.Ok(await reads.ListPincodesAsync(id, includeRemoved ?? false, ct));
            })
            .RequirePermission(ManagePermission);

        admin.MapPost("/service-areas/{id:guid}/pincodes", async (Guid id, AddPincodesRequest request, ServiceAreaService service, HttpContext http, CancellationToken ct) =>
                Respond(http, await service.AddPincodesAsync(id, IfMatch.GetRequiredVersion(http.Request), request, ct), r => r.Version))
            .RequirePermission(ManagePermission);

        admin.MapDelete("/service-areas/{id:guid}/pincodes/{pincode}", async (Guid id, string pincode, ServiceAreaService service, HttpContext http, CancellationToken ct) =>
                Respond(http, await service.RemovePincodeAsync(id, IfMatch.GetRequiredVersion(http.Request), pincode, ct), a => a.Version))
            .RequirePermission(ManagePermission);
    }

    /// <summary>
    /// Reference lookups every staff screen needs (G1, G2): any staff token, whatever its permissions. Names only;
    /// nothing here is a price, a stock figure or customer data.
    /// </summary>
    private static void MapStaffLookups(RouteGroupBuilder admin)
    {
        admin.MapGet("/me/locations", async ([AsParameters] PagedRequest paging, ILocationsReads reads, ICurrentActor actor, CancellationToken ct) =>
            {
                var (items, total) = await reads.ListStaffLocationsAsync(actor.IsAllLocations ? null : actor.LocationIds, paging.Skip, paging.PageSize, ct);
                return TypedResults.Ok(new PagedResponse<StaffLocationItem>(items, paging.Page, paging.PageSize, total));
            })
            .WithValidation<PagedRequest>()
            .WithTags("Admin lookups")
            .RequireStaffActor();

        admin.MapGet("/cities/lookup", async ([AsParameters] PagedRequest paging, [FromQuery(Name = "status")] string[]? status, ILocationsReads reads, CancellationToken ct) =>
            {
                var (items, total) = await reads.LookupCitiesAsync(CityStatuses(status), paging.Skip, paging.PageSize, ct);
                return TypedResults.Ok(new PagedResponse<CityLookupItem>(items, paging.Page, paging.PageSize, total));
            })
            .WithValidation<PagedRequest>()
            .WithTags("Admin lookups")
            .RequireStaffActor();
    }

    /// <summary>The repeatable <c>status</c> query as UPPER_SNAKE codes (rule 8); an unknown code is refused on <c>status</c>.</summary>
    private static List<CityStatus> CityStatuses(string[]? codes)
    {
        var statuses = new List<CityStatus>();
        foreach (var code in codes ?? [])
        {
            statuses.Add(EnumCodes.TryToEnum<CityStatus>(code, out var value)
                ? value
                : throw new RefusalException(new FieldError("status", LocationsErrors.ValidationFailed, $"Status must be one of {string.Join(", ", EnumCodes.AllCodes<CityStatus>())}.")));
        }

        return statuses;
    }

    private static Ok<T> Respond<T>(HttpContext http, T body, Func<T, string> version)
    {
        SetETag(http.Response, version(body));
        return TypedResults.Ok(body);
    }

    private static void SetETag(HttpResponse response, string version)
        => response.SetETag(uint.Parse(version, NumberStyles.None, CultureInfo.InvariantCulture));

    private static NotFoundException LocationNotFound() => new(LocationsErrors.FulfillmentLocationNotFound, "No fulfillment location has this id.");

    private static NotFoundException AreaNotFound() => new(LocationsErrors.ServiceAreaNotFound, "No service area has this id.");
}
