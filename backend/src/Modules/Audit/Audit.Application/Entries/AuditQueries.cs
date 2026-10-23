using System.Text.Json;
using FluentValidation;
using FreshOrbit.SharedKernel.Actors;

namespace FreshOrbit.Audit.Application.Entries;

/// <summary>Query string of <c>GET /admin/audit</c> (AUD-R5).</summary>
public sealed record AuditListRequest(
    string? EntityType,
    string? EntityId,
    Guid? ActorId,
    string? Action,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page = 1,
    int PageSize = 20);

public sealed record AuditEntryResponse(
    long Id,
    DateTimeOffset OccurredAt,
    ActorType ActorType,
    Guid? ActorId,
    string? ActorName,
    string Action,
    string EntityType,
    string EntityId,
    JsonElement? Before,
    JsonElement? After,
    string? Ip,
    string? CorrelationId);

public sealed record AuditListResponse(IReadOnlyList<AuditEntryResponse> Items, int Page, int PageSize, int TotalCount);

public sealed class AuditListRequestValidator : AbstractValidator<AuditListRequest>
{
    public AuditListRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("PAGE_OUT_OF_RANGE");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithErrorCode("PAGE_SIZE_OUT_OF_RANGE");
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate).When(x => x.FromDate is not null && x.ToDate is not null)
            .WithErrorCode("DATE_RANGE_INVALID").WithMessage("The toDate must not be before fromDate.");
    }
}

/// <summary>Read side of the audit log.</summary>
public interface IAuditReads
{
    Task<AuditListResponse> ListAsync(AuditListRequest request, CancellationToken cancellationToken);
}
