using System.Globalization;
using FreshOrbit.Ordering.Application.Checkout;
using FreshOrbit.Ordering.Application.Complaints;
using FreshOrbit.Ordering.Application.Orders;
using FreshOrbit.Ordering.Application.Shared;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.Idempotency;
using FreshOrbit.SharedKernel.Web.Paging;
using FreshOrbit.SharedKernel.Web.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Ordering.Infrastructure.Endpoints;

/// <summary>Checkout, customer order and admin order / complaint endpoints (docs/api §4.8, §4.15).</summary>
internal static class OrderingEndpoints
{
    public const string OrdersView = "orders.view";
    public const string OrdersPack = "orders.pack";
    public const string OrdersCancel = "orders.cancel";
    public const string RefundsRequest = "refunds.request";
    public const string ComplaintsManage = "complaints.manage";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var root = endpoints.MapGroup(string.Empty).AddEndpointFilter<AccessProblemFilter>();
        MapCustomer(root);
        MapAdmin(root);
    }

    private static void MapCustomer(RouteGroupBuilder root)
    {
        root.MapPost("/checkout/preview", async (CheckoutRequest request, ICurrentActor actor, CheckoutService service, CancellationToken ct)
                => TypedResults.Ok(await service.PreviewAsync(CustomerOf(actor), request, ct)))
            .RequireRateLimiting(RateLimitPolicies.Checkout)
            .WithTags("Checkout");

        var orders = root.MapGroup("/orders").WithTags("Orders");
        orders.MapPost(string.Empty, PlaceOrderAsync).WithIdempotency(required: true).RequireRateLimiting(RateLimitPolicies.Checkout);
        orders.MapGet(string.Empty, async ([FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, ICurrentActor actor, OrderQueryService queries, CancellationToken ct) =>
        {
            var (p, size) = Paging(page, pageSize);
            var (items, total) = await queries.ListCustomerOrdersAsync(CustomerOf(actor), status, p, size, ct);
            return TypedResults.Ok(new PagedResponse<OrderListItem>(items, p, size, total));
        });
        orders.MapGet("/{orderId:guid}", async (Guid orderId, ICurrentActor actor, OrderQueryService queries, HttpResponse response, CancellationToken ct)
            => WithETag(response, await queries.GetCustomerOrderAsync(CustomerOf(actor), orderId, ct)));
        orders.MapGet("/{orderId:guid}/tracking", async (Guid orderId, ICurrentActor actor, OrderQueryService queries, CancellationToken ct)
            => TypedResults.Ok(await queries.TrackingAsync(CustomerOf(actor), orderId, ct)));
        orders.MapPost("/{orderId:guid}/cancel", async (Guid orderId, CancelOrderRequest request, ICurrentActor actor, OrderCommandService commands, HttpResponse response, CancellationToken ct)
                => WithETag(response, await commands.CancelByCustomerAsync(CustomerOf(actor), orderId, request, ct)))
            .WithIdempotency(required: true);
        orders.MapPost("/{orderId:guid}/substitutions/{substitutionId:guid}/accept", async (Guid orderId, Guid substitutionId, ICurrentActor actor, OrderCommandService commands, HttpResponse response, CancellationToken ct)
            => WithETag(response, await commands.AnswerSubstitutionAsync(CustomerOf(actor), orderId, substitutionId, accept: true, ct)));
        orders.MapPost("/{orderId:guid}/substitutions/{substitutionId:guid}/reject", async (Guid orderId, Guid substitutionId, ICurrentActor actor, OrderCommandService commands, HttpResponse response, CancellationToken ct)
            => WithETag(response, await commands.AnswerSubstitutionAsync(CustomerOf(actor), orderId, substitutionId, accept: false, ct)));
        orders.MapPost("/{orderId:guid}/complaints", async (Guid orderId, RaiseComplaintRequest request, ICurrentActor actor, ComplaintService complaints, CancellationToken ct) =>
            {
                var complaint = await complaints.RaiseAsync(CustomerOf(actor), orderId, request, ct);
                return TypedResults.Created($"/api/v1/orders/{orderId}/complaints/{complaint.Id}", complaint);
            })
            .WithIdempotency(required: true);
        orders.MapGet("/{orderId:guid}/complaints", async (Guid orderId, ICurrentActor actor, ComplaintService complaints, CancellationToken ct)
            => TypedResults.Ok(await complaints.ListForOrderAsync(CustomerOf(actor), orderId, ct)));
        orders.MapGet("/{orderId:guid}/invoice", async (Guid orderId, ICurrentActor actor, OrderQueryService queries, CancellationToken ct)
            => TypedResults.Ok(await queries.CustomerInvoiceAsync(CustomerOf(actor), orderId, ct)));
        orders.MapPost("/{orderId:guid}/reorder", async (Guid orderId, ICurrentActor actor, OrderCommandService commands, CancellationToken ct)
            => TypedResults.Ok(await commands.ReorderAsync(CustomerOf(actor), orderId, ct)));
    }

    private static void MapAdmin(RouteGroupBuilder root)
    {
        var orders = root.MapGroup("/admin/orders").WithTags("Admin orders");
        orders.MapGet(string.Empty, async (
                [FromQuery] Guid? fulfillmentLocationId,
                [FromQuery] string? status,
                [FromQuery] string? slotDate,
                [FromQuery] Guid? slotId,
                [FromQuery] string? paymentMethod,
                [FromQuery] string? q,
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                ICurrentActor actor,
                OrderQueryService queries,
                CancellationToken ct) =>
            {
                var (p, size) = Paging(page, pageSize);
                var query = new AdminOrderListQuery(fulfillmentLocationId, status, ParseDate(slotDate), slotId, paymentMethod, q, p, size);
                var (items, total) = await queries.AdminListAsync(actor, query, ct);
                return TypedResults.Ok(new PagedResponse<AdminOrderListItem>(items, p, size, total));
            })
            .RequirePermission(OrdersView);
        orders.MapGet("/pick-list", async ([FromQuery] Guid fulfillmentLocationId, [FromQuery] Guid slotId, ICurrentActor actor, OrderQueryService queries, CancellationToken ct)
                => TypedResults.Ok(await queries.PickListAsync(actor, fulfillmentLocationId, slotId, ct)))
            .RequirePermission(OrdersPack);
        orders.MapGet("/{orderId:guid}", async (Guid orderId, ICurrentActor actor, OrderQueryService queries, HttpResponse response, CancellationToken ct)
                => WithETag(response, await queries.AdminDetailAsync(actor, orderId, ct)))
            .RequirePermission(OrdersView);
        orders.MapPost("/{orderId:guid}/start-preparing", async (Guid orderId, HttpRequest request, ICurrentActor actor, OrderCommandService commands, CancellationToken ct)
                => WithETag(request.HttpContext.Response, await commands.StartPreparingAsync(actor, orderId, IfMatch.GetRequiredVersion(request), ct)))
            .RequirePermission(OrdersPack);
        orders.MapPost("/{orderId:guid}/pack", async (Guid orderId, PackOrderRequest body, HttpRequest request, ICurrentActor actor, OrderCommandService commands, CancellationToken ct) =>
            {
                var packed = await commands.PackAsync(actor, orderId, IfMatch.GetRequiredVersion(request), body, ct);
                SetETag(request.HttpContext.Response, packed.Version);
                return TypedResults.Ok(packed);
            })
            .RequirePermission(OrdersPack);
        orders.MapPost("/{orderId:guid}/items/{itemId:guid}/substitutions", async (Guid orderId, Guid itemId, ProposeSubstitutionRequest body, HttpRequest request, ICurrentActor actor, OrderCommandService commands, CancellationToken ct)
                => WithETag(request.HttpContext.Response, await commands.ProposeSubstitutionAsync(actor, orderId, itemId, IfMatch.GetRequiredVersion(request), body, ct)))
            .RequirePermission(OrdersPack);
        orders.MapPost("/{orderId:guid}/substitutions/{substitutionId:guid}/record-response", async (Guid orderId, Guid substitutionId, RecordSubstitutionResponseRequest body, HttpRequest request, ICurrentActor actor, OrderCommandService commands, CancellationToken ct)
                => WithETag(request.HttpContext.Response, await commands.RecordSubstitutionResponseAsync(actor, orderId, substitutionId, IfMatch.GetRequiredVersion(request), body, ct)))
            .RequirePermission(OrdersPack);
        orders.MapPost("/{orderId:guid}/cancel", async (Guid orderId, CancelOrderRequest body, HttpRequest request, ICurrentActor actor, OrderCommandService commands, CancellationToken ct)
                => WithETag(request.HttpContext.Response, await commands.CancelByStaffAsync(actor, orderId, IfMatch.GetRequiredVersion(request), body, ct)))
            .RequirePermission(OrdersCancel);
        orders.MapPost("/{orderId:guid}/goodwill", async (Guid orderId, GoodwillRequest body, HttpRequest request, ICurrentActor actor, OrderCommandService commands, CancellationToken ct)
                => WithETag(request.HttpContext.Response, await commands.GoodwillAsync(actor, orderId, IfMatch.GetRequiredVersion(request), body, ct)))
            .RequirePermission(RefundsRequest);
        orders.MapGet("/{orderId:guid}/invoice", async (Guid orderId, ICurrentActor actor, OrderQueryService queries, CancellationToken ct)
                => TypedResults.Ok(await queries.StaffInvoiceAsync(actor, orderId, ct)))
            .RequirePermission(OrdersView);

        var complaints = root.MapGroup("/admin/complaints").RequirePermission(ComplaintsManage).WithTags("Admin complaints");
        complaints.MapGet(string.Empty, async ([FromQuery] string? status, [FromQuery] string? kind, [FromQuery] Guid? fulfillmentLocationId, [FromQuery] int? page, [FromQuery] int? pageSize, ICurrentActor actor, ComplaintService service, CancellationToken ct) =>
        {
            var (p, size) = Paging(page, pageSize);
            var (items, total) = await service.AdminListAsync(actor, status, kind, fulfillmentLocationId, p, size, ct);
            return TypedResults.Ok(new PagedResponse<ComplaintResponse>(items, p, size, total));
        });
        complaints.MapGet("/{complaintId:guid}", async (Guid complaintId, ICurrentActor actor, ComplaintService service, HttpResponse response, CancellationToken ct)
            => WithETag(response, ComplaintResponse.From(await service.GetForStaffAsync(actor, complaintId, ct))));
        complaints.MapPost("/{complaintId:guid}/decide", async (Guid complaintId, DecideComplaintRequest body, HttpRequest request, ICurrentActor actor, ComplaintService service, CancellationToken ct)
            => WithETag(request.HttpContext.Response, await service.DecideAsync(actor, complaintId, IfMatch.GetRequiredVersion(request), body, ct)));
        complaints.MapPost("/{complaintId:guid}/resolve", async (Guid complaintId, HttpRequest request, ICurrentActor actor, ComplaintService service, CancellationToken ct)
            => WithETag(request.HttpContext.Response, await service.ResolveAsync(actor, complaintId, IfMatch.GetRequiredVersion(request), ct)));
    }

    private static async Task<IResult> PlaceOrderAsync(CheckoutRequest request, HttpRequest http, ICurrentActor actor, CheckoutService service, CancellationToken ct)
    {
        var customerId = CustomerOf(actor);
        var key = http.Headers[IdempotencyFilter.HeaderName].ToString();
        var outcome = await service.PlaceAsync(customerId, request, key, ct);
        if (outcome.PriceChangedPreview is { } preview)
        {
            // Returned (not thrown) so the idempotency record stores it: confirming the new price needs a new key (ORD-R10).
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict.",
                detail: "Prices changed since you last saw them; review the new total and place the order again.",
                extensions: new Dictionary<string, object?> { ["code"] = OrderingErrors.PriceChanged, ["preview"] = preview });
        }

        var order = outcome.Order!;
        SetETag(http.HttpContext.Response, order.Version);
        return TypedResults.Created($"/api/v1/orders/{order.Id}", order);
    }

    private static Guid CustomerOf(ICurrentActor actor)
        => actor.CustomerId ?? throw OrderingAccessException.LoginRequired();

    private static Microsoft.AspNetCore.Http.HttpResults.Ok<OrderDetailResponse> WithETag(HttpResponse response, OrderDetailResponse detail)
    {
        SetETag(response, detail.Version);
        return TypedResults.Ok(detail);
    }

    private static Microsoft.AspNetCore.Http.HttpResults.Ok<AdminOrderDetailResponse> WithETag(HttpResponse response, AdminOrderDetailResponse detail)
    {
        SetETag(response, detail.Order.Version);
        return TypedResults.Ok(detail);
    }

    private static Microsoft.AspNetCore.Http.HttpResults.Ok<ComplaintResponse> WithETag(HttpResponse response, ComplaintResponse complaint)
    {
        SetETag(response, complaint.Version);
        return TypedResults.Ok(complaint);
    }

    private static void SetETag(HttpResponse response, string version)
    {
        if (uint.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            response.SetETag(value);
        }
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new RefusalException(new FieldError("slotDate", OrderingErrors.ValidationFailed, "Use the date format yyyy-MM-dd."));
    }

    private static (int Page, int PageSize) Paging(int? page, int? pageSize)
    {
        var p = page ?? 1;
        var size = pageSize ?? PagedRequest.DefaultPageSize;
        var errors = new List<FieldError>();
        if (p < 1)
        {
            errors.Add(new FieldError("page", OrderingErrors.ValidationFailed, "Page must be 1 or more."));
        }

        if (size is < 1 or > PagedRequest.MaxPageSize)
        {
            errors.Add(new FieldError("pageSize", OrderingErrors.ValidationFailed, $"Page size must be between 1 and {PagedRequest.MaxPageSize}."));
        }

        return errors.Count > 0 ? throw new RefusalException(errors) : (p, size);
    }
}

/// <summary>Turns <see cref="OrderingAccessException"/> into 401 / 403 Problem Details with its code.</summary>
internal sealed class AccessProblemFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(context);
        }
        catch (OrderingAccessException denied)
        {
            return TypedResults.Problem(
                statusCode: denied.StatusCode,
                title: denied.StatusCode == StatusCodes.Status401Unauthorized ? "Unauthorized." : "Forbidden.",
                detail: denied.Message,
                extensions: new Dictionary<string, object?> { ["code"] = denied.Code });
        }
    }
}
