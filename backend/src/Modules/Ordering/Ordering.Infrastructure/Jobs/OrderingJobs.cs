using FreshOrbit.Ordering.Application.Events;
using FreshOrbit.SharedKernel.Jobs;

namespace FreshOrbit.Ordering.Infrastructure.Jobs;

/// <summary>
/// Every minute: unpaid ONLINE orders past <c>payment_due_at</c> → PAYMENT_FAILED (ORD-R45). Returns at once when the
/// partial index on <c>payment_due_at</c> has nothing due.
/// </summary>
public sealed class ExpireUnpaidOrdersJob(OrderExpiryService expiry) : IRecurringJob
{
    public Task ExecuteAsync(CancellationToken cancellationToken) => expiry.ExpireUnpaidOrdersAsync(cancellationToken);
}

/// <summary>Every minute: PROPOSED substitutions past <c>expires_at</c> → EXPIRED, line short-supplied (ORD-R36).</summary>
public sealed class ExpireSubstitutionsJob(OrderExpiryService expiry) : IRecurringJob
{
    public Task ExecuteAsync(CancellationToken cancellationToken) => expiry.ExpireSubstitutionsAsync(cancellationToken);
}

internal sealed class OrderingJobs : IRecurringJobRegistration
{
    public IEnumerable<RecurringJobDefinition> GetJobs() =>
    [
        new("ordering.expire-unpaid-orders", "* * * * *", typeof(ExpireUnpaidOrdersJob)),
        new("ordering.expire-substitutions", "* * * * *", typeof(ExpireSubstitutionsJob)),
        new(ComplaintPhotoPurgeJob.JobId, "45 3 * * *", typeof(ComplaintPhotoPurgeJob)),
    ];
}
