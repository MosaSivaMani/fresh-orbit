using System.Globalization;
using FreshOrbit.Customers.Application.Addresses;
using FreshOrbit.Customers.Application.Admin;
using FreshOrbit.Customers.Application.Profiles;
using FreshOrbit.Customers.Application.Wishlist;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Customers.Infrastructure.Endpoints;

/// <summary>Customer endpoints under <c>/api/v1/me</c> and staff endpoints under <c>/api/v1/admin/customers</c>.</summary>
internal static class CustomersEndpoints
{
    public const string ViewPermission = "customers.view";
    public const string ManagePermission = "customers.manage";
    private const string CustomerActorCode = "CUSTOMER";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapGroup("/me")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim(FreshOrbitClaimTypes.ActorType, CustomerActorCode))
            .WithTags("Customers");

        me.MapGet(string.Empty, async (IProfileService service, HttpResponse response, CancellationToken ct) =>
        {
            var profile = await service.GetAsync(ct);
            response.SetETag(Parse(profile.Version));
            return TypedResults.Ok(profile);
        });

        me.MapPatch(string.Empty, async (UpdateProfileRequest request, IProfileService service, HttpContext http, CancellationToken ct) =>
        {
            var profile = await service.UpdateAsync(request, IfMatch.GetRequiredVersion(http.Request), ct);
            http.Response.SetETag(Parse(profile.Version));
            return TypedResults.Ok(profile);
        }).WithValidation<UpdateProfileRequest>();

        me.MapDelete(string.Empty, async (IProfileService service, HttpRequest request, CancellationToken ct) =>
        {
            await service.DeleteAccountAsync(IfMatch.GetRequiredVersion(request), ct);
            return TypedResults.NoContent();
        });

        me.MapGet("/addresses", async (IAddressService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(ct)));

        me.MapPost("/addresses", async (SaveAddressRequest request, IAddressService service, CancellationToken ct) =>
        {
            var address = await service.AddAsync(request, ct);
            return TypedResults.Created($"/api/v1/me/addresses/{address.AddressId}", address);
        });

        me.MapPut("/addresses/{addressId:guid}", async (Guid addressId, SaveAddressRequest request, IAddressService service, HttpContext http, CancellationToken ct) =>
        {
            var address = await service.ReplaceAsync(addressId, request, IfMatch.GetRequiredVersion(http.Request), ct);
            http.Response.SetETag(Parse(address.Version));
            return TypedResults.Ok(address);
        });

        me.MapDelete("/addresses/{addressId:guid}", async (Guid addressId, IAddressService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(addressId, ct);
            return TypedResults.NoContent();
        });

        me.MapPost("/addresses/{addressId:guid}/default", async (Guid addressId, IAddressService service, CancellationToken ct)
            => TypedResults.Ok(await service.MakeDefaultAsync(addressId, ct)));

        me.MapGet("/wishlist", async ([FromQuery] string? pincode, [FromHeader(Name = "X-Pincode")] string? pincodeHeader, IWishlistService service, CancellationToken ct)
                => TypedResults.Ok(await service.ListAsync(string.IsNullOrWhiteSpace(pincode) ? pincodeHeader : pincode, ct)))
            .Produces<IReadOnlyList<WishlistItemResponse>>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        me.MapPut("/wishlist/{productId:guid}", async (Guid productId, IWishlistService service, CancellationToken ct) =>
        {
            await service.AddAsync(productId, ct);
            return TypedResults.NoContent();
        });

        me.MapDelete("/wishlist/{productId:guid}", async (Guid productId, IWishlistService service, CancellationToken ct) =>
        {
            await service.RemoveAsync(productId, ct);
            return TypedResults.NoContent();
        });

        var admin = endpoints.MapGroup("/admin/customers").WithTags("Customers");

        admin.MapGet(string.Empty, async ([AsParameters] CustomerListRequest request, IAdminCustomerService service, CancellationToken ct)
            => TypedResults.Ok(await service.ListAsync(request, ct)))
            .WithValidation<CustomerListRequest>()
            .RequirePermission(ViewPermission);

        admin.MapGet("/{customerId:guid}", async (Guid customerId, IAdminCustomerService service, HttpResponse response, CancellationToken ct) =>
        {
            var customer = await service.GetAsync(customerId, ct);
            response.SetETag(Parse(customer.Version));
            return TypedResults.Ok(customer);
        }).RequirePermission(ViewPermission);

        admin.MapPost("/{customerId:guid}/block", (Guid customerId, IAdminCustomerService service, HttpContext http, CancellationToken ct)
            => SetBlockedAsync(customerId, true, service, http, ct)).RequirePermission(ManagePermission).Produces<CustomerDetailResponse>();

        admin.MapPost("/{customerId:guid}/unblock", (Guid customerId, IAdminCustomerService service, HttpContext http, CancellationToken ct)
            => SetBlockedAsync(customerId, false, service, http, ct)).RequirePermission(ManagePermission).Produces<CustomerDetailResponse>();
    }

    private static async Task<IResult> SetBlockedAsync(Guid customerId, bool blocked, IAdminCustomerService service, HttpContext http, CancellationToken ct)
    {
        var customer = await service.SetBlockedAsync(customerId, blocked, IfMatch.GetRequiredVersion(http.Request), ct);
        http.Response.SetETag(Parse(customer.Version));
        return TypedResults.Ok(customer);
    }

    private static uint Parse(string version) => uint.Parse(version, CultureInfo.InvariantCulture);
}
