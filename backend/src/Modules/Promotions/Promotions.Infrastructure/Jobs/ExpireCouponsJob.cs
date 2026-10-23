using FreshOrbit.Promotions.Application.Abstractions;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Promotions.Infrastructure.Jobs;

/// <summary>
/// Hourly tidy-up: ACTIVE / PAUSED coupons whose <c>valid_to</c> has passed become EXPIRED and raise
/// CouponStatusChanged. Validity is always decided from dates, so this only keeps lists honest. One save per batch
/// of 500; returns at once when nothing is due (partial index on <c>valid_to</c>).
/// </summary>
public sealed class ExpireCouponsJob(FreshOrbitDbContext db, ICouponRepository coupons, IClock clock) : IRecurringJob
{
    public const int BatchSize = 500;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var now = clock.UtcNow;
            var due = await coupons.ListDueForExpiryAsync(now, BatchSize, cancellationToken);
            if (due.Count == 0)
            {
                return;
            }

            foreach (var coupon in due)
            {
                coupon.ExpireIfDue(now);
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

internal sealed class PromotionsJobs : IRecurringJobRegistration
{
    public IEnumerable<RecurringJobDefinition> GetJobs() => [new("promotions.expire-coupons", "5 * * * *", typeof(ExpireCouponsJob))];
}
