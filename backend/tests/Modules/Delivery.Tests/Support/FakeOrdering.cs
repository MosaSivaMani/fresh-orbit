using System.Collections.Concurrent;
using FreshOrbit.Ordering.Public;

namespace FreshOrbit.Delivery.Tests.Support;

/// <summary>
/// Hand-written stand-in for Ordering as Delivery reads it: the order facts the shipment board shows (AD-1, AD-2).
/// Shipments in these tests come from events without an order row, so other questions have no order to answer.
/// </summary>
public sealed class FakeOrdering : IOrderingApi
{
    private readonly ConcurrentDictionary<Guid, OrderDeliveryFacts> _facts = new();

    public void AddOrder(OrderDeliveryFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        _facts[facts.OrderId] = facts;
    }

    public Task<IReadOnlyDictionary<Guid, OrderDeliveryFacts>> GetOrderDeliveryFactsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderIds);
        IReadOnlyDictionary<Guid, OrderDeliveryFacts> found = orderIds.Distinct()
            .Where(_facts.ContainsKey)
            .ToDictionary(id => id, id => _facts[id]);
        return Task.FromResult(found);
    }

    public Task<OrderSummary?> GetOrderSummaryAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult<OrderSummary?>(null);

    public Task<bool> HasQualifyingOrderAsync(Guid customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<OrderPaymentFacts?> GetOrderPaymentFactsAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult<OrderPaymentFacts?>(null);

    public Task<OrderStatus?> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult<OrderStatus?>(null);
}
