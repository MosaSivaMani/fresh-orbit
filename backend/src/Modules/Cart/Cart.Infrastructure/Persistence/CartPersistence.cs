using FreshOrbit.Cart.Application.Abstractions;
using FreshOrbit.Cart.Domain.Carts;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;

namespace FreshOrbit.Cart.Infrastructure.Persistence;

internal sealed class ShoppingCartConfiguration : IEntityTypeConfiguration<ShoppingCart>
{
    public void Configure(EntityTypeBuilder<ShoppingCart> builder)
    {
        builder.ToTable("carts", t =>
        {
            t.HasCheckConstraint("ck_carts_status", $"status IN ({string.Join(", ", EnumCodes.AllCodes<CartStatus>().Select(c => $"'{c}'"))})");
            t.HasCheckConstraint("ck_carts_owner", "customer_id IS NOT NULL OR guest_token_hash IS NOT NULL");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.GuestTokenHash).HasMaxLength(64);
        builder.Property(x => x.Pincode).HasMaxLength(10);
        builder.Property(x => x.CouponCode).HasMaxLength(20);
        builder.HasIndex(x => x.CustomerId).IsUnique().HasFilter("status = 'ACTIVE'");
        builder.HasIndex(x => x.GuestTokenHash).IsUnique();
        builder.HasIndex(x => x.ExpiresAt).HasFilter("status = 'ACTIVE'");
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.CartId, x.VariantId, x.PreparationMethodId }).IsUnique().AreNullsDistinct(false);
    }
}

internal sealed class CartRepository(FreshOrbitDbContext db) : ICartRepository
{
    public Task<ShoppingCart?> FindActiveByCustomerAsync(Guid customerId, CancellationToken cancellationToken)
        => db.Set<ShoppingCart>().Include(c => c.Items).FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status == CartStatus.Active, cancellationToken);

    public Task<ShoppingCart?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => db.Set<ShoppingCart>().Include(c => c.Items).FirstOrDefaultAsync(c => c.GuestTokenHash == tokenHash, cancellationToken);

    public Task<ShoppingCart?> FindByIdAsync(Guid cartId, CancellationToken cancellationToken)
        => db.Set<ShoppingCart>().Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == cartId, cancellationToken);

    public async Task<IReadOnlyList<ShoppingCart>> ListDueForAbandonAsync(DateTimeOffset now, int take, CancellationToken cancellationToken)
        => await db.Set<ShoppingCart>()
            .Include(c => c.Items)
            .Where(c => c.Status == CartStatus.Active && c.ExpiresAt <= now)
            .OrderBy(c => c.ExpiresAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public void Add(ShoppingCart cart) => db.Add(cart);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        db.TouchChangedAggregates();
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Retries a cart command from Read after a lost race (CART-R19): 3 retries, then CONCURRENT_SAVE.</summary>
internal sealed class CartCommandRunner(FreshOrbitDbContext db) : ICartCommandRunner
{
    public const int MaxRetries = 3;

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await command(cancellationToken);
            }
            catch (ConcurrentSaveException) when (attempt < MaxRetries)
            {
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                db.ChangeTracker.Clear();
                if (attempt >= MaxRetries)
                {
                    throw new ConcurrentSaveException(exception);
                }
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

internal static class AggregateTouch
{
    /// <summary>
    /// Marks an unchanged aggregate root as modified when one of its child rows was added, changed or deleted, so
    /// the root's row version (<c>xmin</c>) is checked and bumped by the same save.
    /// </summary>
    public static void TouchChangedAggregates(this DbContext db)
    {
        db.ChangeTracker.DetectChanges();
        var changedChildren = db.ChangeTracker.Entries()
            .Where(e => e.Entity is not IAggregateRoot && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        foreach (var child in changedChildren)
        {
            foreach (var foreignKey in child.Metadata.GetForeignKeys().Where(fk => typeof(IAggregateRoot).IsAssignableFrom(fk.PrincipalEntityType.ClrType)))
            {
                var keyValue = child.Property(foreignKey.Properties[0].Name).CurrentValue ?? child.Property(foreignKey.Properties[0].Name).OriginalValue;
                var root = db.ChangeTracker.Entries()
                    .FirstOrDefault(e => e.Metadata == foreignKey.PrincipalEntityType && Equals(e.Property(foreignKey.PrincipalKey.Properties[0].Name).CurrentValue, keyValue));
                if (root is { State: EntityState.Unchanged })
                {
                    root.State = EntityState.Modified;
                }
            }
        }
    }
}
