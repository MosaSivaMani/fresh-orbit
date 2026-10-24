using System.Globalization;
using FreshOrbit.Identity.Application.Auth;
using FreshOrbit.Identity.Application.Roles;
using FreshOrbit.Identity.Application.Staff;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.RateLimiting;
using FreshOrbit.SharedKernel.Web.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Identity.Infrastructure.Endpoints;

/// <summary>docs/api §4.1 (auth) and §4.21 (staff, roles, permissions).</summary>
internal static class IdentityEndpoints
{
    public const string StaffManage = "staff.manage";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        MapCustomerAuth(endpoints.MapGroup("/auth").WithTags("Auth"));
        MapStaffAuth(endpoints.MapGroup("/admin/auth").WithTags("Staff auth"));
        MapStaff(endpoints.MapGroup("/admin/staff").WithTags("Staff").RequirePermission(StaffManage));
        MapRoles(endpoints.MapGroup("/admin").WithTags("Roles").RequirePermission(StaffManage));
    }

    private static void MapCustomerAuth(RouteGroupBuilder group)
    {
        group.MapPost("/otp/request", async (OtpRequestRequest request, IOtpService service, CancellationToken ct)
                => TypedResults.Accepted((string?)null, await service.RequestAsync(request, ct)))
            .WithValidation<OtpRequestRequest>()
            .RequireRateLimiting(RateLimitPolicies.OtpRequest)
            .AllowAnonymous();

        group.MapPost("/otp/verify", async (OtpVerifyRequest request, IOtpService service, CancellationToken ct)
                => TypedResults.Ok(await service.VerifyAsync(request, ct)))
            .WithValidation<OtpVerifyRequest>()
            .RequireRateLimiting(RateLimitPolicies.OtpVerify)
            .AllowAnonymous();

        group.MapPost("/refresh", async (RefreshRequest request, ITokenService service, CancellationToken ct)
                => TypedResults.Ok(await service.RefreshAsync(request, ct)))
            .WithValidation<RefreshRequest>()
            .RequireRateLimiting(RateLimitPolicies.Refresh)
            .AllowAnonymous();

        group.MapPost("/logout", async (LogoutRequest request, ITokenService service, ICurrentActor actor, CancellationToken ct) =>
        {
            await service.LogoutAsync(actor.UserId!.Value, request, ct);
            return TypedResults.NoContent();
        }).RequireCustomer();
    }

    private static void MapStaffAuth(RouteGroupBuilder group)
    {
        group.MapPost("/login", async (StaffLoginRequest request, IStaffAuthService service, CancellationToken ct)
                => TypedResults.Ok(await service.LoginAsync(request, ct)))
            .WithValidation<StaffLoginRequest>()
            .RequireRateLimiting(RateLimitPolicies.Login)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous();

        group.MapPost("/logout", async (LogoutRequest request, ITokenService service, ICurrentActor actor, CancellationToken ct) =>
        {
            await service.LogoutAsync(actor.UserId!.Value, request, ct);
            return TypedResults.NoContent();
        }).RequireStaff();

        group.MapGet("/me", async (IStaffAuthService service, ICurrentActor actor, CancellationToken ct)
                => TypedResults.Ok(await service.GetProfileAsync(actor.StaffMemberId ?? throw new NotFoundException("No staff member is signed in."), ct)))
            .RequireStaff();

        group.MapPost("/password", async (ChangePasswordRequest request, IStaffAuthService service, ICurrentActor actor, CancellationToken ct) =>
        {
            await service.ChangePasswordAsync(actor.UserId!.Value, request, ct);
            return TypedResults.NoContent();
        }).WithValidation<ChangePasswordRequest>().RequireStaff();
    }

    private static void MapStaff(RouteGroupBuilder group)
    {
        group.MapGet("", async ([AsParameters] StaffListRequest request, IStaffReads reads, CancellationToken ct) => TypedResults.Ok(await reads.ListAsync(request, ct)))
            .WithValidation<StaffListRequest>();

        group.MapPost("", async (CreateStaffRequest request, IStaffService service, HttpResponse response, CancellationToken ct) =>
        {
            var staff = await service.CreateAsync(request, ct);
            SetETag(response, staff.Version);
            return TypedResults.Created($"/api/v1/admin/staff/{staff.Id}", staff);
        }).WithValidation<CreateStaffRequest>();

        group.MapGet("/{staffId:guid}", async (Guid staffId, IStaffReads reads, HttpResponse response, CancellationToken ct) =>
        {
            var staff = await reads.GetAsync(staffId, ct) ?? throw new NotFoundException("No staff member has this id.");
            SetETag(response, staff.Version);
            return TypedResults.Ok(staff);
        });

        group.MapPut("/{staffId:guid}", async (Guid staffId, UpdateStaffRequest request, IStaffService service, HttpContext http, CancellationToken ct) =>
        {
            var staff = await service.UpdateAsync(staffId, IfMatch.GetRequiredVersion(http.Request), request, ct);
            SetETag(http.Response, staff.Version);
            return TypedResults.Ok(staff);
        }).WithValidation<UpdateStaffRequest>();

        group.MapPost("/{staffId:guid}/reset-password", async (Guid staffId, ResetStaffPasswordRequest request, IStaffService service, HttpContext http, CancellationToken ct) =>
        {
            var staff = await service.ResetPasswordAsync(staffId, IfMatch.GetRequiredVersion(http.Request), request, ct);
            SetETag(http.Response, staff.Version);
            return TypedResults.Ok(staff);
        });
    }

    private static void MapRoles(RouteGroupBuilder group)
    {
        group.MapGet("/roles", async (IRoleService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(ct)));

        group.MapPost("/roles", async (CreateRoleRequest request, IRoleService service, HttpResponse response, CancellationToken ct) =>
        {
            var role = await service.CreateAsync(request, ct);
            SetETag(response, role.Version);
            return TypedResults.Created($"/api/v1/admin/roles/{role.Id}", role);
        }).WithValidation<CreateRoleRequest>();

        group.MapPut("/roles/{roleId:guid}", async (Guid roleId, UpdateRoleRequest request, IRoleService service, HttpContext http, CancellationToken ct) =>
        {
            var role = await service.UpdateAsync(roleId, IfMatch.GetRequiredVersion(http.Request), request, ct);
            SetETag(http.Response, role.Version);
            return TypedResults.Ok(role);
        }).WithValidation<UpdateRoleRequest>();

        group.MapDelete("/roles/{roleId:guid}", async (Guid roleId, IRoleService service, HttpRequest request, CancellationToken ct) =>
        {
            await service.DeleteAsync(roleId, IfMatch.GetRequiredVersion(request), ct);
            return TypedResults.NoContent();
        });

        group.MapGet("/permissions", async (IRoleService service, CancellationToken ct) => TypedResults.Ok(await service.ListPermissionsAsync(ct)));
    }

    private static void SetETag(HttpResponse response, string version) => response.SetETag(uint.Parse(version, CultureInfo.InvariantCulture));
}
