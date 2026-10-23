using FreshOrbit.Payments.Application.Reconciliation;
using FreshOrbit.Payments.Application.Refunds;
using FreshOrbit.Payments.Domain.Events;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Events;
using FreshOrbit.SharedKernel.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Payments.Infrastructure.Jobs;

/// <summary>Recurring jobs of the Payments module.</summary>
internal sealed class PaymentsJobRegistration : IRecurringJobRegistration
{
    public const string ReconciliationJobId = "payments.reconcile";

    public IEnumerable<RecurringJobDefinition> GetJobs() => [new RecurringJobDefinition(ReconciliationJobId, "7 * * * *", typeof(ReconciliationJob))];
}

/// <summary>
/// Hourly reconciliation (PAY-R21). Runs as a single instance: a Postgres advisory lock held for the run makes a
/// concurrent run on another Worker exit immediately.
/// </summary>
public sealed class ReconciliationJob(FreshOrbitDbContext db, ReconciliationService reconciliation) : IRecurringJob
{
    private const long LockKey = 0x5041595245434F4E; // "PAYRECON"

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var acquired = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock({LockKey}) AS \"Value\"").SingleAsync(cancellationToken);
            if (!acquired)
            {
                return;
            }

            try
            {
                await reconciliation.RunAsync(cancellationToken);
            }
            finally
            {
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({LockKey})", CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}

/// <summary>
/// PAY-H9: an approved ORIGINAL refund is sent to the provider by the Worker. The two commits of
/// <see cref="RefundProcessor"/> run in their own DI scope, so this handler leaves the dispatcher's context untouched.
/// A transient provider error fails the handler; the outbox retries it (and reconciliation flags it when stuck).
/// </summary>
public sealed class RefundApprovedHandler(IServiceScopeFactory scopes) : IEventHandler<RefundApproved>
{
    public async Task HandleAsync(RefundApproved domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!string.Equals(domainEvent.Method, "ORIGINAL", StringComparison.Ordinal))
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RefundProcessor>().ProcessAsync(domainEvent.RefundId, cancellationToken);
    }
}
