using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.Payments.Domain.Settlements;
using FreshOrbit.Payments.Domain.Webhooks;
using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Payments.Application.Common;

/// <summary>The command's unit of work, with unique violations surfaced as <see cref="UniqueViolationException"/>.</summary>
public interface IPaymentsSession
{
    /// <summary>The one save of a command. A lost optimistic race throws <c>ConcurrentSaveException</c>.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Forgets every tracked change so a command can retry from Read.</summary>
    void Reset();

    /// <summary>412 when the quoted If-Match version is stale.</summary>
    void SetExpectedVersion(IAggregateRoot aggregate, uint expectedVersion);
}

/// <summary>A save hit a unique index (<see cref="IndexName"/>), e.g. a concurrent duplicate webhook.</summary>
public sealed class UniqueViolationException(string? indexName, Exception? innerException)
    : Exception($"Unique index {indexName} was violated.", innerException)
{
    public string? IndexName { get; } = indexName;
}

public interface IPaymentRepository
{
    void Add(Payment payment);

    Task<Payment?> GetAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<Payment?> GetByRefundIdAsync(Guid refundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Payment>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Payment>> ListByOrdersAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken);

    Task<Payment?> FindByProviderPaymentIdAsync(string provider, string providerPaymentId, CancellationToken cancellationToken);

    Task<Payment?> FindByProviderOrderIdAsync(string provider, string providerOrderId, CancellationToken cancellationToken);

    Task<Payment?> FindByProviderRefundIdAsync(string provider, string providerRefundId, CancellationToken cancellationToken);

    /// <summary>COLLECTED CASH payments of a rider collected in [fromUtc, toUtc) and not yet settled.</summary>
    Task<IReadOnlyList<Payment>> ListUnsettledCashAsync(Guid riderId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    /// <summary>Online PENDING/AUTHORIZED attempts created in [createdFrom, createdTo), oldest first.</summary>
    Task<IReadOnlyList<Payment>> ListOpenOnlineAttemptsAsync(DateTimeOffset createdFrom, DateTimeOffset createdTo, int limit, CancellationToken cancellationToken);

    /// <summary>Payments with a refund PROCESSING since before <paramref name="approvedBefore"/>.</summary>
    Task<IReadOnlyList<Payment>> ListWithProcessingRefundsAsync(DateTimeOffset approvedBefore, int limit, CancellationToken cancellationToken);

    /// <summary>COD/UPI-on-delivery payments still PENDING whose order was delivered before <paramref name="deliveredBefore"/>.</summary>
    Task<IReadOnlyList<Payment>> ListDeliveredNotCollectedAsync(DateTimeOffset deliveredBefore, CancellationToken cancellationToken);

    /// <summary>CASH collections without a settlement collected before <paramref name="collectedBefore"/>.</summary>
    Task<IReadOnlyList<Payment>> ListUnsettledCashBeforeAsync(DateTimeOffset collectedBefore, CancellationToken cancellationToken);
}

public interface IWebhookEventRepository
{
    void Add(WebhookEvent webhookEvent);

    Task<WebhookEvent?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Whether a signature-valid event with this id was already stored.</summary>
    Task<bool> ExistsAsync(string provider, string providerEventId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WebhookEvent>> ListErrorsAsync(int limit, CancellationToken cancellationToken);
}

public interface ICashSettlementRepository
{
    void Add(CashSettlement settlement);

    Task<CashSettlement?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid riderId, DateOnly settlementDate, CancellationToken cancellationToken);

    /// <summary>OPEN or DISPUTED settlements dated before <paramref name="before"/>.</summary>
    Task<IReadOnlyList<CashSettlement>> ListUnresolvedBeforeAsync(DateOnly before, CancellationToken cancellationToken);
}

public interface IReconciliationIssueRepository
{
    void Add(ReconciliationIssue issue);

    Task<ReconciliationIssue?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ReconciliationIssue?> FindOpenAsync(IssueKind kind, Guid subjectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReconciliationIssue>> ListOpenAsync(IReadOnlyCollection<IssueKind> kinds, CancellationToken cancellationToken);
}

/// <summary>
/// Shipment and rider facts owned by Delivery. <c>Delivery.Public</c> does not expose them yet; the manager wires
/// this port to <c>IDeliveryApi</c> at merge (see the Phase 2 report).
/// </summary>
public interface IShipmentFacts
{
    Task<ShipmentFacts?> GetShipmentAsync(Guid shipmentId, CancellationToken cancellationToken);

    Task<RiderFacts?> GetRiderAsync(Guid riderId, CancellationToken cancellationToken);
}

public sealed record ShipmentFacts(Guid ShipmentId, Guid OrderId, Guid? RiderId, Guid FulfillmentLocationId);

/// <param name="TimeZone">IANA time zone of the rider's location (settlement dates are local dates).</param>
public sealed record RiderFacts(Guid RiderId, Guid FulfillmentLocationId, string TimeZone, bool IsActive);
