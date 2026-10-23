using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.Payments.Domain.Settlements;
using FreshOrbit.Payments.Domain.Webhooks;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshOrbit.Payments.Infrastructure.Persistence.Repositories;

internal sealed class PaymentsSession(FreshOrbitDbContext db, IUnitOfWork unitOfWork) : IPaymentsSession
{
    private const string UniqueViolation = "23505";

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolation } postgres)
        {
            throw new UniqueViolationException(postgres.ConstraintName, exception);
        }
    }

    public void Reset() => db.ChangeTracker.Clear();

    public void SetExpectedVersion(IAggregateRoot aggregate, uint expectedVersion) => unitOfWork.SetExpectedVersion(aggregate, expectedVersion);
}

internal sealed class PaymentRepository(FreshOrbitDbContext db) : IPaymentRepository
{
    private IQueryable<Payment> Payments => db.Set<Payment>().Include(p => p.Refunds);

    public void Add(Payment payment) => db.Add(payment);

    public Task<Payment?> GetAsync(Guid paymentId, CancellationToken cancellationToken)
        => Payments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

    public Task<Payment?> GetByRefundIdAsync(Guid refundId, CancellationToken cancellationToken)
        => Payments.FirstOrDefaultAsync(p => p.Refunds.Any(r => r.Id == refundId), cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken)
        => await Payments.Where(p => p.OrderId == orderId).OrderBy(p => p.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListByOrdersAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
        => await Payments.Where(p => orderIds.Contains(p.OrderId)).OrderBy(p => p.Id).ToListAsync(cancellationToken);

    public Task<Payment?> FindByProviderPaymentIdAsync(string provider, string providerPaymentId, CancellationToken cancellationToken)
        => Payments.FirstOrDefaultAsync(p => p.Provider == provider && p.ProviderPaymentId == providerPaymentId, cancellationToken);

    public Task<Payment?> FindByProviderOrderIdAsync(string provider, string providerOrderId, CancellationToken cancellationToken)
        => Payments.FirstOrDefaultAsync(p => p.Provider == provider && p.ProviderOrderId == providerOrderId, cancellationToken);

    public Task<Payment?> FindByProviderRefundIdAsync(string provider, string providerRefundId, CancellationToken cancellationToken)
        => Payments.FirstOrDefaultAsync(p => p.Provider == provider && p.Refunds.Any(r => r.ProviderRefundId == providerRefundId), cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListUnsettledCashAsync(Guid riderId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
        => await Payments
            .Where(p => p.Status == PaymentStatus.Collected && p.CollectionMode == CollectionMode.Cash && p.CashSettlementId == null
                && p.CollectedByRiderId == riderId && p.CollectedAt >= fromUtc && p.CollectedAt < toUtc)
            .OrderBy(p => p.CollectedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListOpenOnlineAttemptsAsync(DateTimeOffset createdFrom, DateTimeOffset createdTo, int limit, CancellationToken cancellationToken)
        => await Payments
            .Where(p => p.Method == PaymentMethod.Online && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Authorized)
                && EF.Property<DateTimeOffset>(p, AuditColumns.CreatedAt) >= createdFrom
                && EF.Property<DateTimeOffset>(p, AuditColumns.CreatedAt) < createdTo)
            .OrderBy(p => EF.Property<DateTimeOffset>(p, AuditColumns.CreatedAt))
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListWithProcessingRefundsAsync(DateTimeOffset approvedBefore, int limit, CancellationToken cancellationToken)
        => await Payments
            .Where(p => p.Refunds.Any(r => r.Status == RefundStatus.Processing && r.ApprovedAt < approvedBefore))
            .OrderBy(p => p.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListDeliveredNotCollectedAsync(DateTimeOffset deliveredBefore, CancellationToken cancellationToken)
        => await Payments
            .Where(p => (p.Method == PaymentMethod.Cod || p.Method == PaymentMethod.UpiOnDelivery) && p.Status == PaymentStatus.Pending
                && p.OrderDeliveredAt != null && p.OrderDeliveredAt < deliveredBefore)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListUnsettledCashBeforeAsync(DateTimeOffset collectedBefore, CancellationToken cancellationToken)
        => await Payments
            .Where(p => p.Status == PaymentStatus.Collected && p.CollectionMode == CollectionMode.Cash && p.CashSettlementId == null && p.CollectedAt < collectedBefore)
            .ToListAsync(cancellationToken);
}

internal sealed class WebhookEventRepository(FreshOrbitDbContext db) : IWebhookEventRepository
{
    public void Add(WebhookEvent webhookEvent) => db.Add(webhookEvent);

    public Task<WebhookEvent?> GetAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<WebhookEvent>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(string provider, string providerEventId, CancellationToken cancellationToken)
        => db.Set<WebhookEvent>().AnyAsync(e => e.Provider == provider && e.ProviderEventId == providerEventId && e.SignatureValid, cancellationToken);

    public async Task<IReadOnlyList<WebhookEvent>> ListErrorsAsync(int limit, CancellationToken cancellationToken)
        => await db.Set<WebhookEvent>().Where(e => e.Status == WebhookEventStatus.Error).OrderBy(e => e.ReceivedAt).Take(limit).ToListAsync(cancellationToken);
}

internal sealed class CashSettlementRepository(FreshOrbitDbContext db) : ICashSettlementRepository
{
    public void Add(CashSettlement settlement) => db.Add(settlement);

    public Task<CashSettlement?> GetAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<CashSettlement>().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid riderId, DateOnly settlementDate, CancellationToken cancellationToken)
        => db.Set<CashSettlement>().AnyAsync(s => s.RiderId == riderId && s.SettlementDate == settlementDate, cancellationToken);

    public async Task<IReadOnlyList<CashSettlement>> ListUnresolvedBeforeAsync(DateOnly before, CancellationToken cancellationToken)
        => await db.Set<CashSettlement>()
            .Where(s => (s.Status == CashSettlementStatus.Open || s.Status == CashSettlementStatus.Disputed) && s.SettlementDate < before)
            .ToListAsync(cancellationToken);
}

internal sealed class ReconciliationIssueRepository(FreshOrbitDbContext db) : IReconciliationIssueRepository
{
    public void Add(ReconciliationIssue issue) => db.Add(issue);

    public Task<ReconciliationIssue?> GetAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<ReconciliationIssue>().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<ReconciliationIssue?> FindOpenAsync(IssueKind kind, Guid subjectId, CancellationToken cancellationToken)
    {
        // An issue added earlier in the same command is not in the database yet.
        var pending = db.ChangeTracker.Entries<ReconciliationIssue>()
            .Select(e => e.Entity)
            .FirstOrDefault(i => i.Kind == kind && i.SubjectId == subjectId && i.Status == IssueStatus.Open);
        return pending ?? await db.Set<ReconciliationIssue>()
            .FirstOrDefaultAsync(i => i.Kind == kind && i.SubjectId == subjectId && i.Status == IssueStatus.Open, cancellationToken);
    }

    public async Task<IReadOnlyList<ReconciliationIssue>> ListOpenAsync(IReadOnlyCollection<IssueKind> kinds, CancellationToken cancellationToken)
        => await db.Set<ReconciliationIssue>().Where(i => i.Status == IssueStatus.Open && kinds.Contains(i.Kind)).ToListAsync(cancellationToken);
}
