using FreshOrbit.Cart.Application.Pricing;
using FreshOrbit.SharedKernel.Time;
using FreshOrbit.SharedKernel.Web.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Cart.Infrastructure.Endpoints;

/// <summary>
/// <c>GET /price-quote?variantId=&amp;preparation=&amp;quantity=&amp;unit=</c> (docs/api §4.6, CART-R17): estimate and max
/// chargeable of one line before it is added. Public; location from the <c>pincode</c> query or <c>X-Pincode</c>.
/// </summary>
internal static class PriceQuoteEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/price-quote", async (
                Guid? variantId,
                string? preparation,
                int? quantity,
                string? unit,
                string? pincode,
                HttpContext http,
                PriceQuoteService service,
                IClock clock,
                CancellationToken ct) =>
            {
                var context = !string.IsNullOrWhiteSpace(pincode) ? pincode : http.Request.Headers[CartEndpoints.PincodeHeader].ToString();
                return TypedResults.Ok(await service.QuoteAsync(context, variantId, preparation, quantity, unit, clock.UtcNow, ct));
            })
            .WithTags("Cart")
            .Produces<PriceQuoteResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireRateLimiting(RateLimitPolicies.Public);
}
