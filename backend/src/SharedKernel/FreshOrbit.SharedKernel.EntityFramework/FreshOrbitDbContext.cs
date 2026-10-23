using System.Diagnostics;
using System.Text.Json;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Events;
using FreshOrbit.SharedKernel.EntityFramework.Conventions;
using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Serialization;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FreshOrbit.SharedKernel.EntityFramework;

/// <summary>
/// The one DbContext of the monolith. Its model is the union of every registered <see cref="IModuleModel"/>.
/// <see cref="SaveChangesAsync(bool, CancellationToken)"/> stamps audit columns, writes raised domain events to
/// <c>platform.outbox</c> in the same transaction and translates a lost optimistic-concurrency race into
/// <see cref="ConcurrentSaveException"/>.
/// </summary>
public class FreshOrbitDbContext : DbContext, IUnitOfWork
{
    private static readonly string[] Extensions = ["citext", "pg_trgm", "btree_gist"];

    private readonly IReadOnlyList<IModuleModel> _moduleModels;
    private readonly ICurrentActor _actor;
    private readonly IClock _clock;

    public FreshOrbitDbContext(
        DbContextOptions<FreshOrbitDbContext> options,
        IEnumerable<IModuleModel> moduleModels,
        ICurrentActor actor,
        IClock clock)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(moduleModels);
        _moduleModels = [.. moduleModels.DistinctBy(m => m.GetType()).OrderBy(m => m.Schema, StringComparer.Ordinal)];
        _actor = actor;
        _clock = clock;
        ModelKey = string.Join(';', _moduleModels.Select(m => m.GetType().FullName));
    }

    /// <summary>Identifies the set of module models; part of the EF model cache key.</summary>
    public string ModelKey { get; }

    public IReadOnlyList<IModuleModel> ModuleModels => _moduleModels;

    /// <inheritdoc />
    public void SetExpectedVersion(IAggregateRoot aggregate, uint expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var entry = Entry(aggregate);
        if (entry.State is EntityState.Detached or EntityState.Added)
        {
            throw new InvalidOperationException("SetExpectedVersion needs an aggregate loaded by this context.");
        }

        var loaded = (uint)entry.Property(AuditColumns.Version).OriginalValue!;
        if (loaded != expectedVersion)
        {
            throw new ConcurrencyException(expectedVersion, loaded);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var (aggregates, outbox) = PrepareSave();
        try
        {
            var result = base.SaveChanges(acceptAllChangesOnSuccess);
            ClearEvents(aggregates);
            return result;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            DetachOutbox(outbox);
            throw new ConcurrentSaveException(exception);
        }
        catch
        {
            DetachOutbox(outbox);
            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var (aggregates, outbox) = PrepareSave();
        try
        {
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            ClearEvents(aggregates);
            return result;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            DetachOutbox(outbox);
            throw new ConcurrentSaveException(exception);
        }
        catch
        {
            DetachOutbox(outbox);
            throw;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Domain events live in memory on aggregates and in platform.outbox; they are never mapped as entities.
        modelBuilder.Ignore<DomainEvent>();
        foreach (var extension in Extensions)
        {
            modelBuilder.HasPostgresExtension(extension);
        }

        var schemas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var moduleModel in _moduleModels)
        {
            if (!schemas.Add(moduleModel.Schema))
            {
                throw new InvalidOperationException($"Schema '{moduleModel.Schema}' is claimed by more than one module model.");
            }

            var before = modelBuilder.Model.GetEntityTypes().ToHashSet();
            moduleModel.Configure(modelBuilder);
            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(e => !before.Contains(e)))
            {
                if (entityType.GetSchema() is null && entityType.GetViewName() is null)
                {
                    entityType.SetSchema(moduleModel.Schema);
                }
            }
        }

        ModelConventions.Apply(modelBuilder);
    }

    private (List<IAggregateRoot> Aggregates, List<OutboxMessage> Outbox) PrepareSave()
    {
        ChangeTracker.DetectChanges();
        var now = _clock.UtcNow;
        var actorId = _actor.UserId;

        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    SetIfPresent(entry, AuditColumns.CreatedAt, now);
                    SetIfPresent(entry, AuditColumns.CreatedBy, actorId);
                    break;
                case EntityState.Modified:
                    SetIfPresent(entry, AuditColumns.ModifiedAt, now);
                    SetIfPresent(entry, AuditColumns.ModifiedBy, actorId);
                    if (entry.Metadata.FindProperty(AuditColumns.CreatedAt) is not null)
                    {
                        entry.Property(AuditColumns.CreatedAt).IsModified = false;
                        entry.Property(AuditColumns.CreatedBy).IsModified = false;
                    }

                    break;
            }
        }

        var aggregates = ChangeTracker.Entries<IAggregateRoot>()
            .Where(e => e.State != EntityState.Detached && e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();
        var correlationId = Activity.Current?.TraceId.ToHexString();
        var outbox = new List<OutboxMessage>();
        foreach (var domainEvent in aggregates.SelectMany(a => a.DomainEvents))
        {
            var type = domainEvent.GetType();
            var message = new OutboxMessage(
                domainEvent.EventId,
                EventTypeNames.Format(type),
                JsonSerializer.Serialize(domainEvent, type, FreshOrbitJson.Options),
                domainEvent.OccurredAt,
                correlationId);
            outbox.Add(message);
            var outboxEntry = Add(message);
            SetIfPresent(outboxEntry, AuditColumns.CreatedAt, now);
            SetIfPresent(outboxEntry, AuditColumns.CreatedBy, actorId);
        }

        return (aggregates, outbox);
    }

    private static void SetIfPresent(EntityEntry entry, string propertyName, object? value)
    {
        if (entry.Metadata.FindProperty(propertyName) is not null)
        {
            entry.Property(propertyName).CurrentValue = value;
        }
    }

    private static void ClearEvents(List<IAggregateRoot> aggregates)
    {
        foreach (var aggregate in aggregates)
        {
            aggregate.ClearEvents();
        }
    }

    private void DetachOutbox(List<OutboxMessage> outbox)
    {
        foreach (var message in outbox)
        {
            Entry(message).State = EntityState.Detached;
        }
    }
}
