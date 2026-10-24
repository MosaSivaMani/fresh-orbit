using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Delivery.Application.Reads;
using FreshOrbit.Delivery.Application.Riders;
using FreshOrbit.Delivery.Application.Shipments;
using FreshOrbit.Delivery.Application.Slots;
using FreshOrbit.Delivery.Application.Templates;
using FreshOrbit.Delivery.Domain.Shared;
using FreshOrbit.Delivery.Domain.Shipments;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.Paging;
using FreshOrbit.SharedKernel.Web.RateLimiting;
using FreshOrbit.SharedKernel.Web.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Delivery.Infrastructure.Endpoints;

/// <summary>docs/api §4.5 (delivery slots) and §4.17 (admin delivery).</summary>
internal static class DeliveryEndpoints
{
    public const string Permission = "delivery.manage";
    public const string CashSettlePermission = "cash.settle";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/delivery-slots", GetCustomerSlotsAsync).RequireRateLimiting(RateLimitPolicies.Public).AllowAnonymous()
            .Produces<CustomerSlotsResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // G7: a minimal rider picker for cash settlement; readable with delivery.manage or cash.settle, so it sits
        // outside the delivery.manage group.
        endpoints.MapGet("/admin/delivery/riders/lookup", LookupRidersAsync)
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim(FreshOrbitClaimTypes.Permission, Permission, CashSettlePermission))
            .Produces<PagedResponse<RiderLookupItem>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var admin = endpoints.MapGroup("/admin/delivery").RequirePermission(Permission);

        admin.MapGet("/slot-templates", ListTemplatesAsync).Produces<PagedResponse<SlotTemplateResponse>>();
        admin.MapPost("/slot-templates", CreateTemplateAsync).WithValidation<CreateSlotTemplateRequest>().Produces<SlotTemplateResponse>(StatusCodes.Status201Created);
        admin.MapPut("/slot-templates/{id:guid}", UpdateTemplateAsync).WithValidation<UpdateSlotTemplateRequest>().Produces<SlotTemplateResponse>();

        admin.MapGet("/slots", ListSlotsAsync).Produces<PagedResponse<DeliverySlotResponse>>();
        admin.MapPatch("/slots/{slotId:guid}", ChangeSlotAsync).Produces<DeliverySlotResponse>();

        admin.MapGet("/riders", ListRidersAsync).Produces<PagedResponse<RiderResponse>>();
        admin.MapPost("/riders", CreateRiderAsync).WithValidation<CreateRiderRequest>().Produces<RiderResponse>(StatusCodes.Status201Created);
        admin.MapPut("/riders/{riderId:guid}", UpdateRiderAsync).WithValidation<UpdateRiderRequest>().Produces<RiderResponse>();

        admin.MapGet("/shipments", ListShipmentsAsync).Produces<PagedResponse<ShipmentResponse>>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        admin.MapGet("/shipments/{shipmentId:guid}", GetShipmentAsync).Produces<ShipmentResponse>();
        admin.MapPost("/shipments/{shipmentId:guid}/assign", AssignAsync).WithValidation<AssignRiderRequest>().Produces<ShipmentResponse>();
        admin.MapPost("/shipments/{shipmentId:guid}/dispatch", DispatchAsync).WithValidation<DispatchShipmentRequest>().Produces<ShipmentResponse>();
        admin.MapPost("/shipments/{shipmentId:guid}/deliver", DeliverAsync).WithValidation<DeliverShipmentRequest>().Produces<ShipmentResponse>();
        admin.MapPost("/shipments/{shipmentId:guid}/attempts", RecordAttemptAsync).Produces<ShipmentResponse>();
        admin.MapPost("/shipments/{shipmentId:guid}/reattempt", ReattemptAsync).Produces<ShipmentResponse>();
        admin.MapPost("/shipments/{shipmentId:guid}/return", ReturnAsync).Produces<ShipmentResponse>();
    }

    // ---- customer -------------------------------------------------------------------------------------------

    private static async Task<IResult> GetCustomerSlotsAsync(
        [FromQuery] string? pincode,
        [FromHeader(Name = "X-Pincode")] string? pincodeHeader,
        [FromQuery] DateOnly? date,
        [FromServices] ILocationsApi locations,
        IDeliveryReads reads,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var code = string.IsNullOrWhiteSpace(pincode) ? pincodeHeader : pincode;
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException(DeliveryErrors.LocationRequired, "Send the delivery pincode in the pincode query or the X-Pincode header.", "pincode");
        }

        var served = await locations.ResolvePincodeAsync(code.Trim(), cancellationToken);
        if (!served.IsServiceable || served.FulfillmentLocationId is not { } locationId || served.TimeZone is null)
        {
            throw new DomainException(DeliveryErrors.PincodeNotServiceable, "We do not deliver to this pincode yet; check the pincode.", "pincode");
        }

        var dates = SlotSchedule.HorizonDates(clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(served.TimeZone));
        var (from, to) = date is { } day ? (day, day) : (dates[0], dates[^1]);
        IReadOnlyList<CustomerSlotResponse> items = from < dates[0] || to > dates[^1]
            ? []
            : await reads.ListCustomerSlotsAsync(locationId, from, to, cancellationToken);
        return TypedResults.Ok(new CustomerSlotsResponse(locationId, items, served.TimeZone));
    }

    // ---- slot templates ---------------------------------------------------------------------------------------

    private static async Task<IResult> ListTemplatesAsync(Guid? fulfillmentLocationId, int? page, int? pageSize, IDeliveryReads reads, ICurrentActor actor, CancellationToken cancellationToken)
    {
        var (p, size) = Paging(page, pageSize);
        var result = await reads.ListTemplatesAsync(new TemplateQuery(fulfillmentLocationId, actor.ScopedLocations(), (p - 1) * size, size), cancellationToken);
        return TypedResults.Ok(new PagedResponse<SlotTemplateResponse>(result.Items, p, size, result.TotalCount));
    }

    private static async Task<IResult> CreateTemplateAsync(CreateSlotTemplateRequest request, SlotTemplateService service, FreshOrbitDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var template = await SaveSupport.RetryAsync(db, () => service.CreateAsync(request, cancellationToken));
        response.SetETag(template.Version);
        return TypedResults.Created($"/api/v1/admin/delivery/slot-templates/{template.Id}", template.ToResponse());
    }

    private static async Task<IResult> UpdateTemplateAsync(Guid id, UpdateSlotTemplateRequest request, SlotTemplateService service, HttpRequest http, CancellationToken cancellationToken)
    {
        var version = IfMatch.GetRequiredVersion(http);
        var template = await SaveSupport.MapAsync(() => service.UpdateAsync(id, request, version, cancellationToken));
        http.HttpContext.Response.SetETag(template.Version);
        return TypedResults.Ok(template.ToResponse());
    }

    // ---- slots ------------------------------------------------------------------------------------------------

    private static async Task<IResult> ListSlotsAsync(Guid? fulfillmentLocationId, DateOnly? fromDate, DateOnly? toDate, int? page, int? pageSize, IDeliveryReads reads, ICurrentActor actor, CancellationToken cancellationToken)
    {
        var (p, size) = Paging(page, pageSize);
        var result = await reads.ListSlotsAsync(new SlotQuery(fulfillmentLocationId, fromDate, toDate, actor.ScopedLocations(), (p - 1) * size, size), cancellationToken);
        return TypedResults.Ok(new PagedResponse<DeliverySlotResponse>(result.Items, p, size, result.TotalCount));
    }

    private static async Task<IResult> ChangeSlotAsync(Guid slotId, ChangeSlotRequest request, SlotService service, IClock clock, HttpRequest http, CancellationToken cancellationToken)
    {
        var version = IfMatch.GetRequiredVersion(http);
        var slot = await service.ChangeAsync(slotId, request, version, cancellationToken);
        http.HttpContext.Response.SetETag(slot.Version);
        return TypedResults.Ok(slot.ToResponse(clock.UtcNow));
    }

    // ---- riders -----------------------------------------------------------------------------------------------

    private static async Task<IResult> ListRidersAsync(Guid? fulfillmentLocationId, bool? isActive, int? page, int? pageSize, IDeliveryReads reads, ICurrentActor actor, CancellationToken cancellationToken)
    {
        var (p, size) = Paging(page, pageSize);
        var result = await reads.ListRidersAsync(new RiderQuery(fulfillmentLocationId, isActive, actor.ScopedLocations(), (p - 1) * size, size), cancellationToken);
        return TypedResults.Ok(new PagedResponse<RiderResponse>(result.Items, p, size, result.TotalCount));
    }

    private static async Task<IResult> LookupRidersAsync(Guid? fulfillmentLocationId, bool? isActive, string? q, int? page, int? pageSize, IDeliveryReads reads, ICurrentActor actor, CancellationToken cancellationToken)
    {
        var (p, size) = Paging(page, pageSize);
        if (fulfillmentLocationId is { } locationId)
        {
            actor.EnsureLocation(locationId);
        }

        var result = await reads.LookupRidersAsync(new RiderLookupQuery(fulfillmentLocationId, isActive, q, actor.ScopedLocations(), (p - 1) * size, size), cancellationToken);
        return TypedResults.Ok(new PagedResponse<RiderLookupItem>(result.Items, p, size, result.TotalCount));
    }

    private static async Task<IResult> CreateRiderAsync(CreateRiderRequest request, RiderService service, FreshOrbitDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var rider = await SaveSupport.RetryAsync(db, () => service.CreateAsync(request, cancellationToken));
        response.SetETag(rider.Version);
        return TypedResults.Created($"/api/v1/admin/delivery/riders/{rider.Id}", rider.ToResponse());
    }

    private static async Task<IResult> UpdateRiderAsync(Guid riderId, UpdateRiderRequest request, RiderService service, HttpRequest http, CancellationToken cancellationToken)
    {
        var version = IfMatch.GetRequiredVersion(http);
        var rider = await SaveSupport.MapAsync(() => service.UpdateAsync(riderId, request, version, cancellationToken));
        http.HttpContext.Response.SetETag(rider.Version);
        return TypedResults.Ok(rider.ToResponse());
    }

    // ---- shipments --------------------------------------------------------------------------------------------

    private static async Task<IResult> ListShipmentsAsync(
        Guid? fulfillmentLocationId,
        Guid? slotId,
        Guid? riderId,
        [FromQuery] string[]? status,
        DateOnly? slotDate,
        string? sort,
        int? page,
        int? pageSize,
        IDeliveryReads reads,
        ICurrentActor actor,
        CancellationToken cancellationToken)
    {
        var (p, size) = Paging(page, pageSize);
        var statuses = new List<ShipmentStatus>();
        foreach (var code in status ?? [])
        {
            statuses.Add(EnumCodes.TryToEnum<ShipmentStatus>(code, out var value)
                ? value
                : throw DeliveryErrors.Invalid("status", "Status must be a shipment status code such as OUT_FOR_DELIVERY."));
        }

        var query = new ShipmentQuery(fulfillmentLocationId, slotId, riderId, statuses, actor.ScopedLocations(), (p - 1) * size, size)
        {
            SlotDate = slotDate,
            Sort = ShipmentSort.Parse(sort),
        };
        var result = await reads.ListShipmentsAsync(query, cancellationToken);
        return TypedResults.Ok(new PagedResponse<ShipmentResponse>(result.Items, p, size, result.TotalCount));
    }

    private static async Task<IResult> GetShipmentAsync(Guid shipmentId, IDeliveryReads reads, ICurrentActor actor, HttpResponse response, CancellationToken cancellationToken)
    {
        var shipment = await reads.GetShipmentAsync(shipmentId, cancellationToken) ?? throw new NotFoundException("No shipment has this id.");
        actor.EnsureLocation(shipment.FulfillmentLocationId);
        response.Headers.ETag = $"\"{shipment.Version}\"";
        return TypedResults.Ok(shipment);
    }

    private static Task<IResult> AssignAsync(Guid shipmentId, AssignRiderRequest request, ShipmentService service, HttpRequest http, CancellationToken cancellationToken)
        => ShipmentCommandAsync(http, version => service.AssignAsync(shipmentId, request, version, cancellationToken));

    private static Task<IResult> DispatchAsync(Guid shipmentId, DispatchShipmentRequest request, ShipmentService service, HttpRequest http, CancellationToken cancellationToken)
        => ShipmentCommandAsync(http, version => service.DispatchAsync(shipmentId, request, version, cancellationToken));

    private static Task<IResult> DeliverAsync(Guid shipmentId, DeliverShipmentRequest request, ShipmentService service, HttpRequest http, CancellationToken cancellationToken)
        => ShipmentCommandAsync(http, version => service.DeliverAsync(shipmentId, request, version, cancellationToken));

    private static Task<IResult> RecordAttemptAsync(Guid shipmentId, RecordAttemptRequest request, ShipmentService service, HttpRequest http, CancellationToken cancellationToken)
        => ShipmentCommandAsync(http, version => service.RecordFailedAttemptAsync(shipmentId, request, version, cancellationToken));

    private static Task<IResult> ReattemptAsync(Guid shipmentId, ShipmentService service, HttpRequest http, CancellationToken cancellationToken)
        => ShipmentCommandAsync(http, version => service.ReattemptAsync(shipmentId, version, cancellationToken));

    private static Task<IResult> ReturnAsync(Guid shipmentId, ShipmentService service, HttpRequest http, CancellationToken cancellationToken)
        => ShipmentCommandAsync(http, version => service.ReturnAsync(shipmentId, version, cancellationToken));

    private static async Task<IResult> ShipmentCommandAsync(HttpRequest http, Func<uint, Task<Shipment>> command)
    {
        var version = IfMatch.GetRequiredVersion(http);
        var shipment = await command(version);
        http.HttpContext.Response.SetETag(shipment.Version);
        return TypedResults.Ok(shipment.ToResponse());
    }

    private static (int Page, int PageSize) Paging(int? page, int? pageSize)
    {
        var p = page ?? 1;
        var size = pageSize ?? PagedRequest.DefaultPageSize;
        SaveSupport.Page(p, size);
        return (p, size);
    }
}
