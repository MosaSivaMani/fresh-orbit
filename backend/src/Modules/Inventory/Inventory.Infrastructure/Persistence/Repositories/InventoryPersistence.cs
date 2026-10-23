using FreshOrbit.Inventory.Application.ColdChain;
using FreshOrbit.Inventory.Application.Common;
using FreshOrbit.Inventory.Application.Jobs;
using FreshOrbit.Inventory.Domain.ColdChain;
using FreshOrbit.Inventory.Domain.Shared;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Infrastructure.Persistence.Configurations;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshOrbit.Inventory.Infrastructure.Persistence.Repositories;

/// <summary>
/// The shared DbContext's save with Inventory's constraint translation: a duplicate batch code or stock item becomes
/// a 409 refusal; a deadlock, or a second ACTIVE reservation for one order item, is a lost race (CONCURRENT_SAVE).
/// </summary>
internal sealed class InventoryUnitOfWork(FreshOrbitDbContext db) : IInventoryUnitOfWork
{
    private const string UniqueViolation = "23505";
    private const string Deadlock = "40P01";

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres && Translate(exception, postgres) is not null)
        {
            throw Translate(exception, (PostgresException)exception.InnerException)!;
        }
    }

    public void SetExpectedVersion(IAggregateRoot aggregate, uint expectedVersion) => db.SetExpectedVersion(aggregate, expectedVersion);

    public void ClearChanges() => db.ChangeTracker.Clear();

    private static DomainException? Translate(DbUpdateException exception, PostgresException postgres) => (postgres.SqlState, postgres.ConstraintName) switch
    {
        (Deadlock, _) => new ConcurrentSaveException(exception),
        (UniqueViolation, InventoryConstraints.BatchLocationCode) => new ConflictException(InventoryErrors.BatchCodeTaken, "This batch code is already used at the location.", "batchCode"),
        (UniqueViolation, InventoryConstraints.StockItemLocationVariant) => new ConflictException(InventoryErrors.Duplicate, "A stock item already exists for this variant at this location.", "variantId"),
        (UniqueViolation, InventoryConstraints.ActiveReservationPerItem or InventoryConstraints.DigestPerDay) => new ConcurrentSaveException(exception),
        _ => null,
    };
}

internal sealed class ColdChainRepository(FreshOrbitDbContext db) : IColdChainRepository
{
    public Task<StorageUnitRange?> GetRangeAsync(string storageUnitCode, CancellationToken cancellationToken = default)
        => db.Set<StorageUnitRange>().AsNoTracking().FirstOrDefaultAsync(r => r.Code == storageUnitCode, cancellationToken);

    public void Add(ColdChainLog log) => db.Add(log);
}

internal sealed class InventoryJobQueries(FreshOrbitDbContext db) : IInventoryJobQueries
{
    public async Task<IReadOnlyList<LowStockRow>> GetLowStockAsync(CancellationToken cancellationToken = default)
        => await db.Set<StockItem>().AsNoTracking()
            .Where(s => s.LowStockThreshold > 0 && s.OnHand - s.Reserved <= s.LowStockThreshold)
            .OrderBy(s => s.FulfillmentLocationId).ThenBy(s => s.OnHand - s.Reserved)
            .Select(s => new LowStockRow(s.FulfillmentLocationId, s.Id, s.VariantId, s.OnHand - s.Reserved, s.LowStockThreshold))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<(Guid FulfillmentLocationId, DateOnly LocalDate)>> GetSentDigestsAsync(
        IReadOnlyCollection<Guid> fulfillmentLocationIds,
        DateOnly fromDate,
        CancellationToken cancellationToken = default)
        => (await db.Set<LowStockDigest>().AsNoTracking()
                .Where(d => fulfillmentLocationIds.Contains(d.FulfillmentLocationId) && d.LocalDate >= fromDate)
                .Select(d => new { d.FulfillmentLocationId, d.LocalDate })
                .ToListAsync(cancellationToken))
            .Select(d => (d.FulfillmentLocationId, d.LocalDate))
            .ToHashSet();

    public void Add(LowStockDigest digest) => db.Add(digest);
}
