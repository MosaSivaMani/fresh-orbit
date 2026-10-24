using FreshOrbit.Cart.Application.Carts;
using FreshOrbit.Cart.Domain.Shared;
using FreshOrbit.Cart.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Web.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Cart.Infrastructure.Endpoints;

/// <summary>
/// Cart endpoints (docs/api §4.6). Owner = customer JWT, else <c>X-Cart-Token</c>. Guests reach every route, so the
/// group is under the per-IP <c>public</c> limit; applying a coupon has its own <c>coupon</c> limit against code guessing
/// (docs/api §1.9, SEC-3).
/// </summary>
internal static class CartEndpoints
{
    public const string CartTokenHeader = "X-Cart-Token";
    public const string PincodeHeader = "X-Pincode";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var cart = endpoints.MapGroup("/cart").WithTags("Cart").RequireRateLimiting(RateLimitPolicies.Public);

        cart.MapGet(string.Empty, async (HttpContext http, ICurrentActor actor, CartService service, CancellationToken ct)
            => Ok(http, await service.GetAsync(Owner(http, actor), ct)));

        cart.MapPost("/items", async (AddCartItemRequest request, [FromQuery] string? pincode, HttpContext http, ICurrentActor actor, CartService service, CancellationToken ct)
            => Ok(http, await service.AddItemAsync(Owner(http, actor), request, Pincode(http, pincode), ct)));

        cart.MapPatch("/items/{itemId:guid}", async (Guid itemId, ChangeCartItemRequest request, HttpContext http, ICurrentActor actor, CartService service, CancellationToken ct)
            => Ok(http, await service.ChangeItemAsync(Owner(http, actor), itemId, request, ct)));

        cart.MapDelete("/items/{itemId:guid}", async (Guid itemId, HttpContext http, ICurrentActor actor, CartService service, CancellationToken ct)
            => Ok(http, await service.RemoveItemAsync(Owner(http, actor), itemId, ct)));

        cart.MapDelete(string.Empty, async (HttpContext http, ICurrentActor actor, CartService service, CancellationToken ct)
            => Ok(http, await service.ClearAsync(Owner(http, actor), ct)));

        cart.MapPut("/location", async (CartLocationRequest request, [FromQuery] bool? force, HttpContext http, ICurrentActor actor, CartService service, CancellationToken ct) =>
        {
            var (view, conflicts) = await service.ChangeLocationAsync(Owner(http, actor), request.Pincode, force ?? false, ct);
            return view is not null
                ? Ok(http, view)
                : (IResult)TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict.",
                    detail: "Some lines are not sold at the new location; confirm with force=true to remove them.",
                    extensions: new Dictionary<string, object?> { ["code"] = CartErrors.CartLocationChanged, ["lines"] = conflicts });
        }).Produces<CartView>().ProducesProblem(StatusCodes.Status409Conflict);

        cart.MapPut("/coupon", async (CartCouponRequest request, HttpContext http, ICurrentActor actor, CartService service, CancellationToken ct)
            => actor.CustomerId is { } customerId ? Ok(http, await service.ApplyCouponAsync(customerId, request.Code, ct)) : (IResult)LoginRequired())
            .RequireRateLimiting(RateLimitPolicies.Coupon).Produces<CartView>();

        cart.MapDelete("/coupon", async (HttpContext http, ICurrentActor actor, CartService service, CancellationToken ct)
            => actor.CustomerId is { } customerId ? Ok(http, await service.RemoveCouponAsync(customerId, ct)) : (IResult)LoginRequired()).Produces<CartView>();
    }

    private static CartOwner Owner(HttpContext http, ICurrentActor actor)
        => CartOwner.For(actor.CustomerId, http.Request.Headers[CartTokenHeader].ToString());

    private static string? Pincode(HttpContext http, string? query)
        => !string.IsNullOrWhiteSpace(query) ? query.Trim() : http.Request.Headers[PincodeHeader].ToString() is { Length: > 0 } header ? header.Trim() : null;

    private static Ok<CartView> Ok(HttpContext http, CartView view)
    {
        if (view.CartToken is { } token)
        {
            http.Response.Headers[CartTokenHeader] = token;
        }

        return TypedResults.Ok(view);
    }

    private static ProblemHttpResult LoginRequired() => TypedResults.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Login required.",
        detail: "Sign in to use a coupon.",
        extensions: new Dictionary<string, object?> { ["code"] = CartErrors.LoginRequired });
}
