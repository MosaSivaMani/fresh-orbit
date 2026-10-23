using FreshOrbit.SharedKernel.Web.Auth;
using Microsoft.AspNetCore.Builder;

namespace FreshOrbit.Identity.Infrastructure.Endpoints;

internal static class IdentityEndpointExtensions
{
    public const string CustomerCode = "CUSTOMER";
    public const string StaffCode = "STAFF";

    /// <summary>Only a customer token passes (staff and anonymous callers get 403 / 401).</summary>
    public static TBuilder RequireCustomer<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim(FreshOrbitClaimTypes.ActorType, CustomerCode));

    /// <summary>Only a staff token passes.</summary>
    public static TBuilder RequireStaff<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim(FreshOrbitClaimTypes.ActorType, StaffCode));
}
