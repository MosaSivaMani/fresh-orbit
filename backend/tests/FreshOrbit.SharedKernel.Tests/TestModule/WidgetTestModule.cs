using FluentValidation;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.Idempotency;
using FreshOrbit.SharedKernel.Web.Modules;
using FreshOrbit.SharedKernel.Web.Validation;
using FreshOrbit.SharedKernel.Actors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.SharedKernel.Tests.TestModule;

public sealed record CreateWidgetRequest(string Name);

public sealed record RenameWidgetRequest(string Name);

public sealed record WidgetResponse(Guid Id, string Name, WidgetStatus Status, uint Version);

public sealed class CreateWidgetRequestValidator : AbstractValidator<CreateWidgetRequest>
{
    public CreateWidgetRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(100).WithErrorCode("WIDGET_NAME_TOO_LONG");
    }
}

/// <summary>Endpoints that exercise the web kernel through the real Api pipeline (mapped under /api/v1/test).</summary>
public sealed class WidgetTestModule : IModule
{
    public string Name => "Test";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<CreateWidgetRequest>, CreateWidgetRequestValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/test");

        group.MapPost("/widgets", async (CreateWidgetRequest request, IUnitOfWork unitOfWork, FreshOrbitDbContext db, CancellationToken ct) =>
            {
                var widget = Widget.Create(request.Name);
                db.Add(widget);
                await unitOfWork.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/v1/test/widgets/{widget.Id}", ToResponse(widget));
            })
            .WithValidation<CreateWidgetRequest>()
            .WithIdempotency();

        group.MapGet("/widgets/{id:guid}", async (Guid id, FreshOrbitDbContext db, HttpResponse response, CancellationToken ct) =>
        {
            var widget = await db.Set<Widget>().FirstOrDefaultAsync(w => w.Id == id, ct)
                ?? throw new NotFoundException("WIDGET_NOT_FOUND", "No widget has this id.");
            response.SetETag(widget.Version);
            return TypedResults.Ok(ToResponse(widget));
        });

        group.MapPut("/widgets/{id:guid}", async (Guid id, RenameWidgetRequest request, HttpRequest http, FreshOrbitDbContext db, IUnitOfWork unitOfWork, CancellationToken ct) =>
        {
            var expected = IfMatch.GetRequiredVersion(http);
            var widget = await db.Set<Widget>().FirstOrDefaultAsync(w => w.Id == id, ct)
                ?? throw new NotFoundException("WIDGET_NOT_FOUND", "No widget has this id.");
            unitOfWork.SetExpectedVersion(widget, expected);
            widget.Rename(request.Name);
            await unitOfWork.SaveChangesAsync(ct);
            http.HttpContext.Response.SetETag(widget.Version);
            return TypedResults.Ok(ToResponse(widget));
        });

        group.MapGet("/errors/{kind}", IResult (string kind) => kind switch
        {
            "refusal" => throw new RefusalException([new FieldError("lines[0].quantity", "QUANTITY_BELOW_MINIMUM", "Quantity is below the minimum."), new FieldError("lines[0].quantity", "QUANTITY_NOT_A_STEP", "Quantity is not a multiple of the step.")]),
            "domain" => throw new DomainException("COUPON_EXPIRED", "The coupon has expired.", "couponCode"),
            "not-found" => throw new NotFoundException("ORDER_NOT_FOUND", "No order has this id."),
            "conflict" => throw new ConflictException("ORDER_ALREADY_PACKED", "The order is already packed."),
            "stale" => throw new ConcurrencyException(1, 2),
            "race" => throw new ConcurrentSaveException(null),
            "unauthorized" => throw new UnauthorizedException("INVALID_CREDENTIALS", "The email or password is wrong."),
            "forbidden" => throw new ForbiddenException("LOCATION_ACCESS_DENIED", "You do not have access to this location.", "fulfillmentLocationId"),
            "too-many" => throw new TooManyRequestsException("OTP_RESEND_TOO_SOON", "Wait before asking for another code.", 42),
            "unavailable" => throw new ServiceUnavailableException("PAYMENT_PROVIDER_UNAVAILABLE", "The payment provider cannot be reached."),
            _ => throw new InvalidOperationException("Boom with internal details."),
        });

        group.MapGet("/secure", (ICurrentActor actor) => TypedResults.Ok(new
        {
            actorType = actor.ActorType,
            actor.UserId,
            actor.StaffMemberId,
            permissions = actor.Permissions.Order(StringComparer.Ordinal).ToArray(),
            actor.IsAllLocations,
        }))
            .RequirePermission("orders.pack");
    }

    private static WidgetResponse ToResponse(Widget widget) => new(widget.Id, widget.Name, widget.Status, widget.Version);
}
