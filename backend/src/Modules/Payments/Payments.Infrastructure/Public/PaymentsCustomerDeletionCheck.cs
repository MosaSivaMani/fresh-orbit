using FreshOrbit.Customers.Public;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Payments.Infrastructure.Public;

/// <summary>
/// A customer account cannot be deleted while one of the customer's refunds is REQUESTED, APPROVED or PROCESSING
/// (money still owed, e.g. a COD refund awaiting manual payout). One query.
/// </summary>
internal sealed class PaymentsCustomerDeletionCheck(FreshOrbitDbContext db) : ICustomerDeletionCheck
{
    public async Task<IReadOnlyList<string>> GetBlockingItemsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var open = await db.Set<Payment>().AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .SelectMany(p => p.Refunds)
            .AnyAsync(r => r.Status == RefundStatus.Requested || r.Status == RefundStatus.Approved || r.Status == RefundStatus.Processing, cancellationToken);
        return open ? [CustomerOpenMatters.OpenRefund] : [];
    }
}
