using FreshOrbit.Customers.Domain.Addresses;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.Customers.Domain.Wishlist;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshOrbit.Customers.Infrastructure.Persistence.Repositories;

internal sealed class CustomersRepository(FreshOrbitDbContext db) : ICustomersRepository
{
    public Task<Customer?> GetAsync(Guid customerId, CancellationToken cancellationToken)
        => db.Set<Customer>().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

    public Task<Customer?> FindByUserAsync(Guid userId, CancellationToken cancellationToken)
        => db.Set<Customer>().FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public void Add(Customer customer) => db.Add(customer);

    public async Task<IReadOnlyList<Address>> GetActiveAddressesAsync(Guid customerId, CancellationToken cancellationToken)
        => await db.Set<Address>()
            .Where(a => a.CustomerId == customerId && a.IsActive)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.AddedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Address>> GetAllAddressesAsync(Guid customerId, CancellationToken cancellationToken)
        => await db.Set<Address>().Where(a => a.CustomerId == customerId).ToListAsync(cancellationToken);

    public Task<Address?> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken)
        => db.Set<Address>().FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, cancellationToken);

    public void Add(Address address) => db.Add(address);

    public Task<WishlistItem?> GetWishlistItemAsync(Guid customerId, Guid productId, CancellationToken cancellationToken)
        => db.Set<WishlistItem>().FirstOrDefaultAsync(w => w.CustomerId == customerId && w.ProductId == productId, cancellationToken);

    public async Task<IReadOnlyList<WishlistItem>> GetWishlistAsync(Guid customerId, CancellationToken cancellationToken)
        => await db.Set<WishlistItem>().Where(w => w.CustomerId == customerId).ToListAsync(cancellationToken);

    public void Add(WishlistItem item) => db.Add(item);

    public void Remove(WishlistItem item) => db.Remove(item);

    public Task FlushAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<TResult> InTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (db.Database.CurrentTransaction is not null)
        {
            return await work();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await work();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public bool IsDuplicateKey(Exception exception)
        => exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> SearchAsync(
        string? name,
        IReadOnlyCollection<Guid>? userIds,
        CustomerStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = db.Set<Customer>().AsNoTracking();
        if (userIds is not null)
        {
            query = query.Where(c => userIds.Contains(c.UserId));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = "%" + name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
            query = query.Where(c => c.FullName != null && EF.Functions.ILike(c.FullName, pattern));
        }

        if (status is { } s)
        {
            query = query.Where(c => c.Status == s);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(c => c.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }
}
