using FluentValidation;
using FreshOrbit.Audit.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Paging;
using FreshOrbit.SharedKernel.Web.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Audit.Infrastructure.DeadLetters;

/// <summary>Query string of <c>GET /admin/dead-letters</c>: <c>status</c> OPEN or REPLAYED (both when absent), paging.</summary>
public sealed record DeadLetterListRequest(string? Status, int Page = 1, int PageSize = PagedRequest.DefaultPageSize);

public sealed class DeadLetterListRequestValidator : AbstractValidator<DeadLetterListRequest>
{
    public DeadLetterListRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null or "OPEN" or "REPLAYED")
            .WithErrorCode("VALIDATION_FAILED").WithMessage("The status must be OPEN or REPLAYED.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("PAGE_OUT_OF_RANGE");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagedRequest.MaxPageSize).WithErrorCode("PAGE_SIZE_OUT_OF_RANGE");
    }
}

/// <summary>
/// Outbox dead letters for operations staff (BE-2, D296): list them and replay one once its cause is fixed. The
/// platform rows are changed only by the outbox's <see cref="DeadLetterStore"/>; this endpoint adds the audit entry
/// <c>DEAD_LETTER_REPLAYED</c> and saves both in one <c>SaveChanges</c>.
/// </summary>
public static class DeadLetterEndpoints
{
    public const string ReplayAction = "DEAD_LETTER_REPLAYED";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/admin/dead-letters", async ([AsParameters] DeadLetterListRequest request, DeadLetterStore store, CancellationToken ct) =>
            {
                DeadLetterStatus? status = request.Status switch
                {
                    "OPEN" => DeadLetterStatus.Open,
                    "REPLAYED" => DeadLetterStatus.Replayed,
                    _ => null,
                };
                var (items, total) = await store.ListAsync(status, (request.Page - 1) * request.PageSize, request.PageSize, ct);
                return TypedResults.Ok(new PagedResponse<DeadLetterView>(items, request.Page, request.PageSize, total));
            })
            .WithValidation<DeadLetterListRequest>()
            .RequirePermission(AuditModule.DeadLettersPermission)
            .WithTags("Operations");

        endpoints.MapPost("/admin/dead-letters/{deadLetterId:guid}/replay", async (Guid deadLetterId, DeadLetterStore store, IAuditApi audit, ICurrentActor actor, FreshOrbitDbContext db, CancellationToken ct) =>
            {
                var replayed = await store.ReplayAsync(deadLetterId, actor.UserId, ct);
                audit.Record(new AuditRecord(
                    ReplayAction,
                    "DEAD_LETTER",
                    deadLetterId.ToString(),
                    new { replayed.EventId, replayed.EventType, replayed.Handler, replayedAt = (DateTimeOffset?)null },
                    new { replayed.EventId, replayed.EventType, replayed.Handler, replayed.ReplayedAt }));
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(replayed);
            })
            .RequirePermission(AuditModule.DeadLettersPermission)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Operations");
    }
}
