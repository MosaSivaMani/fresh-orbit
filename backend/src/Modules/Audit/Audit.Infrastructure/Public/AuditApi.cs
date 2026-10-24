using System.Text.Json;
using FreshOrbit.Audit.Domain.Entries;
using FreshOrbit.Audit.Infrastructure.Recording;
using FreshOrbit.Audit.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Serialization;
using FreshOrbit.SharedKernel.Time;
using Microsoft.AspNetCore.Http;

namespace FreshOrbit.Audit.Infrastructure.Public;

/// <summary>Adds audit entries to the shared context; the caller's SaveChanges writes them (AUD-R2, AUD-R3).</summary>
internal sealed class AuditApi(FreshOrbitDbContext db, ICurrentActor actor, IClock clock, IHttpContextAccessor? httpContextAccessor = null) : IAuditApi
{
    public void Record(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var entry = AuditEntry.Create(new AuditEntryData(
            clock.UtcNow,
            actor.ActorType,
            actor.UserId,
            null,
            record.Action,
            record.EntityType,
            record.EntityId,
            Serialize(record.Before),
            Serialize(record.After),
            AuditRequestInfo.Ip(httpContextAccessor?.HttpContext),
            AuditRequestInfo.CorrelationId(httpContextAccessor?.HttpContext)));
        db.Add(entry);
    }

    private static string? Serialize(object? value) => value is null ? null : JsonSerializer.Serialize(value, value.GetType(), FreshOrbitJson.Options);
}
