using FreshOrbit.Audit.Domain.Entries;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Time;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FreshOrbit.Audit.Infrastructure.Recording;

/// <summary>
/// AUD-R6 (SEC-7): during an admin write (<see cref="AdminAuditPolicy"/>) by an authenticated staff member, every
/// change the request saves gets an <c>audit.entries</c> row added to the SAME SaveChanges, so the change and its
/// audit commit or roll back together. Nothing happens outside an HTTP request (Worker, Migrations, tests that
/// build their own context), for customers or for exempt endpoints.
/// </summary>
internal sealed class AdminWriteAuditInterceptor(ICurrentActor actor, IClock clock, IHttpContextAccessor? httpContextAccessor = null) : SaveChangesInterceptor
{
    private readonly List<AuditEntry> _pending = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        AddEntries(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        AddEntries(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        _pending.Clear();
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        DetachPending(eventData.Context);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        DetachPending(eventData.Context);
        return Task.CompletedTask;
    }

    private void AddEntries(DbContext? context)
    {
        var http = httpContextAccessor?.HttpContext;
        if (context is null || http is null || actor.ActorType != ActorType.Staff)
        {
            return;
        }

        var decision = AdminAuditPolicy.Decide(http);
        if (decision.Mode != AdminAuditMode.Audited)
        {
            return;
        }

        // A retried save after a failure starts clean (the failed attempt's entries were detached).
        DetachPending(context);
        var changes = AuditChangeSet.Read(context, decision.Action);
        if (changes.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;
        var ip = AuditRequestInfo.Ip(http);
        var correlationId = AuditRequestInfo.CorrelationId(http);
        foreach (var change in changes)
        {
            var entry = AuditEntry.Create(new AuditEntryData(
                now, actor.ActorType, actor.UserId, null, change.Action, change.EntityType, change.EntityId, change.Before, change.After, ip, correlationId));
            var tracked = context.Add(entry);

            // FreshOrbitDbContext stamps audit columns before the interceptors run; stamp ours the same way.
            tracked.Property(AuditColumns.CreatedAt).CurrentValue = now;
            tracked.Property(AuditColumns.CreatedBy).CurrentValue = actor.UserId;
            _pending.Add(entry);
        }
    }

    private void DetachPending(DbContext? context)
    {
        if (context is not null)
        {
            foreach (var entry in _pending)
            {
                context.Entry(entry).State = EntityState.Detached;
            }
        }

        _pending.Clear();
    }
}
