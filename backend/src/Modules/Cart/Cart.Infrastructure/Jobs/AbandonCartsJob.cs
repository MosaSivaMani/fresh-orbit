using FreshOrbit.Cart.Application.Abstractions;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Cart.Infrastructure.Jobs;

/// <summary>
/// Hourly: ACTIVE carts past <c>expires_at</c> become ABANDONED and raise CartAbandoned, one save per batch of 500.
/// Returns at once when nothing is due (partial index on <c>expires_at</c> where ACTIVE).
/// </summary>
public sealed class AbandonCartsJob(FreshOrbitDbContext db, ICartRepository carts, IClock clock) : IRecurringJob
{
    public const int BatchSize = 500;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var now = clock.UtcNow;
            var due = await carts.ListDueForAbandonAsync(now, BatchSize, cancellationToken);
            if (due.Count == 0)
            {
                return;
            }

            foreach (var cart in due)
            {
                cart.AbandonIfDue(now);
            }

            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
            if (due.Count < BatchSize)
            {
                return;
            }
        }
    }
}

internal sealed class CartJobs : IRecurringJobRegistration
{
    public IEnumerable<RecurringJobDefinition> GetJobs() => [new("cart.abandon-carts", "15 * * * *", typeof(AbandonCartsJob))];
}
