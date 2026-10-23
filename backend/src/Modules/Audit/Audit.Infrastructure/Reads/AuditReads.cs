using System.Text.Json;
using FreshOrbit.Audit.Application.Entries;
using FreshOrbit.Audit.Domain.Entries;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Audit.Infrastructure.Reads;

internal sealed class AuditReads(FreshOrbitDbContext db) : IAuditReads
{
    public async Task<AuditListResponse> ListAsync(AuditListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = db.Set<AuditEntry>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            query = query.Where(e => e.EntityType == request.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityId))
        {
            query = query.Where(e => e.EntityId == request.EntityId);
        }

        if (request.ActorId is { } actorId)
        {
            query = query.Where(e => e.ActorId == actorId);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(e => e.Action == request.Action);
        }

        if (request.FromDate is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(e => e.OccurredAt >= start);
        }

        if (request.ToDate is { } to)
        {
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(e => e.OccurredAt < end);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return new AuditListResponse([.. rows.Select(ToResponse)], request.Page, request.PageSize, total);
    }

    private static AuditEntryResponse ToResponse(AuditEntry e) => new(
        e.Id,
        e.OccurredAt,
        e.ActorType,
        e.ActorId,
        e.ActorName,
        e.Action,
        e.EntityType,
        e.EntityId,
        Parse(e.Before),
        Parse(e.After),
        e.Ip,
        e.CorrelationId);

    private static JsonElement? Parse(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
