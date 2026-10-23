using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Payments;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Payments.Infrastructure.Public;

/// <summary><see cref="IPaymentsApi"/> over the payments tables (no tracking: callers share the DbContext).</summary>
internal sealed class PaymentsApi(FreshOrbitDbContext db) : IPaymentsApi
{
    public async Task<OrderPaymentStatus> GetPaymentStatusAsync(Guid orderId, CancellationToken cancellationToken = default)
        => PaymentSummaries.BuildStatus(orderId, await LoadAsync([orderId], cancellationToken));

    public async Task<OrderPaymentSummary> GetOrderPaymentSummaryAsync(Guid orderId, CancellationToken cancellationToken = default)
        => PaymentSummaries.Build(orderId, await LoadAsync([orderId], cancellationToken));

    public async Task<IReadOnlyDictionary<Guid, OrderPaymentSummary>> GetOrderPaymentSummariesAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderIds);
        var attempts = await LoadAsync(orderIds, cancellationToken);
        return orderIds.Distinct().ToDictionary(id => id, id => PaymentSummaries.Build(id, attempts));
    }

    private async Task<List<Payment>> LoadAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
        => await db.Set<Payment>().AsNoTracking().Include(p => p.Refunds).Where(p => orderIds.Contains(p.OrderId)).ToListAsync(cancellationToken);
}

/// <summary>
/// Placeholder for <see cref="IShipmentFacts"/> until <c>Delivery.Public</c> exposes shipment and rider lookups.
/// The manager replaces this registration at merge with an adapter over <c>IDeliveryApi</c>.
/// </summary>
internal sealed class UnavailableShipmentFacts : IShipmentFacts
{
    public Task<ShipmentFacts?> GetShipmentAsync(Guid shipmentId, CancellationToken cancellationToken)
        => throw new NotSupportedException("Delivery.Public has no shipment lookup yet; wire IShipmentFacts to IDeliveryApi at merge.");

    public Task<RiderFacts?> GetRiderAsync(Guid riderId, CancellationToken cancellationToken)
        => throw new NotSupportedException("Delivery.Public has no rider lookup yet; wire IShipmentFacts to IDeliveryApi at merge.");
}
