using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Ordering.Application.Events;

/// <summary>
/// Sweeps of docs/product/ordering.md §6: unpaid ONLINE orders past <c>payment_due_at</c> (ORD-R45) and PROPOSED
/// substitutions past <c>expires_at</c> (ORD-R36). Returns at once when nothing is due; each order is its own
/// transaction, and a lost race re-reads and skips when the order is no longer due.
/// </summary>
public sealed class OrderExpiryService(IOrderRepository orders, IOrderingTransactions transactions, IUnitOfWork unitOfWork, IClock clock)
{
    public const int BatchSize = 100;

    public async Task<int> ExpireUnpaidOrdersAsync(CancellationToken cancellationToken)
    {
        var due = await orders.ListUnpaidDueAsync(clock.UtcNow, BatchSize, cancellationToken);
        var expired = 0;
        foreach (var orderId in due)
        {
            var changed = await transactions.RunAsync(
                async ct =>
                {
                    var order = await orders.GetAsync(orderId, ct);
                    if (order is null || !order.ExpireIfUnpaid(clock.UtcNow))
                    {
                        return false;
                    }

                    await unitOfWork.SaveChangesAsync(ct);
                    return true;
                },
                cancellationToken);
            expired += changed ? 1 : 0;
        }

        return expired;
    }

    public async Task<int> ExpireSubstitutionsAsync(CancellationToken cancellationToken)
    {
        var due = await orders.ListExpiredSubstitutionsAsync(clock.UtcNow, BatchSize, cancellationToken);
        var expired = 0;
        foreach (var (orderId, substitutionId) in due)
        {
            var changed = await transactions.RunAsync(
                async ct =>
                {
                    var order = await orders.GetAsync(orderId, ct);
                    if (order is null || !order.ExpireSubstitution(substitutionId, clock.UtcNow))
                    {
                        return false;
                    }

                    await unitOfWork.SaveChangesAsync(ct);
                    return true;
                },
                cancellationToken);
            expired += changed ? 1 : 0;
        }

        return expired;
    }
}
