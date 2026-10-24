using System.Globalization;
using FreshOrbit.Notifications.Application.Inbox;
using FreshOrbit.Notifications.Application.Templates;
using FreshOrbit.Notifications.Infrastructure.Senders;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Notifications.Infrastructure.Endpoints;

/// <summary>Customer endpoints under <c>/api/v1/me</c> (§4.10) and template administration (§4.21).</summary>
internal static class NotificationsEndpoints
{
    public const string ManagePermission = "notifications.manage";
    private const string CustomerActorCode = "CUSTOMER";

    /// <summary>
    /// Development/Testing only (SEC-1): the messages the development SMS sender kept for one phone, newest first,
    /// so a developer can read an OTP without it ever being logged. An unencoded <c>+</c> (read as a space) is accepted.
    /// </summary>
    public static void MapDevelopment(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/dev/sms", (string? phone, DevelopmentSmsOutbox outbox) =>
                string.IsNullOrWhiteSpace(phone)
                    ? throw new RefusalException(new FieldError("phone", "VALIDATION_FAILED", "Send the phone query parameter in E.164 form."))
                    : TypedResults.Ok(outbox.For("+" + phone.Trim().TrimStart('+'))))
            .AllowAnonymous()
            .WithTags("Development");
    }

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapGroup("/me")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim(FreshOrbitClaimTypes.ActorType, CustomerActorCode))
            .WithTags("Notifications");

        me.MapGet("/notifications", async ([AsParameters] InboxRequest request, InboxService service, CancellationToken ct)
            => TypedResults.Ok(await service.ListAsync(request, ct))).WithValidation<InboxRequest>();

        me.MapPost("/notifications/{messageId:guid}/read", async (Guid messageId, InboxService service, CancellationToken ct) =>
        {
            await service.MarkReadAsync(messageId, ct);
            return TypedResults.NoContent();
        });

        me.MapPost("/notifications/read-all", async (InboxService service, CancellationToken ct) =>
        {
            await service.MarkAllReadAsync(ct);
            return TypedResults.NoContent();
        });

        me.MapPut("/device-tokens", async (RegisterDeviceTokenRequest request, InboxService service, CancellationToken ct)
            => TypedResults.Ok(await service.RegisterDeviceAsync(request, ct))).WithValidation<RegisterDeviceTokenRequest>();

        me.MapDelete("/device-tokens/{token}", async (string token, InboxService service, CancellationToken ct) =>
        {
            await service.UnregisterDeviceAsync(token, ct);
            return TypedResults.NoContent();
        });

        me.MapGet("/notification-preferences", async (InboxService service, CancellationToken ct) => TypedResults.Ok(await service.GetPreferencesAsync(ct)));

        me.MapPut("/notification-preferences", async (ReplacePreferencesRequest request, InboxService service, CancellationToken ct)
            => TypedResults.Ok(await service.ReplacePreferencesAsync(request, ct))).WithValidation<ReplacePreferencesRequest>();

        var admin = endpoints.MapGroup("/admin/notification-templates").RequirePermission(ManagePermission).WithTags("Notifications");

        admin.MapGet(string.Empty, async (TemplateAdminService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(ct)));

        admin.MapPut("/{code}/{channel}", async (string code, string channel, UpdateTemplateRequest request, TemplateAdminService service, HttpContext http, CancellationToken ct) =>
        {
            var template = await service.UpdateAsync(code, channel, request, IfMatch.GetRequiredVersion(http.Request), ct);
            http.Response.SetETag(uint.Parse(template.Version, CultureInfo.InvariantCulture));
            return TypedResults.Ok(template);
        });

        admin.MapPost("/{code}/{channel}/test", async (string code, string channel, TemplateAdminService service, CancellationToken ct)
            => TypedResults.Ok(await service.TestSendAsync(code, channel, ct)));
    }
}
