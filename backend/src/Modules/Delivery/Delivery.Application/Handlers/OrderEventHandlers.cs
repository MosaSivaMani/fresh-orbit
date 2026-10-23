using FreshOrbit.Delivery.Domain.Shipments;
using FreshOrbit.Delivery.Domain.Slots;

namespace FreshOrbit.Delivery.Application.Handlers;

/// <summary>
/// Delivery's reaction to Ordering events (delivery.md §3 "Events consumed"). Each method only tracks changes:
/// the outbox dispatcher saves them together with the inbox row. Thin <c>IEventHandler&lt;OrderX&gt;</c> adapters
/// map the event fields to these parameters. Every method is idempotent on its own state.
/// </summary>
public sealed class OrderEventHandlers(IShipmentRepository shipments, IDeliverySlotRepository slots)
{
    /// <summary><c>OrderPlaced</c>: shipment PENDING. An existing shipment for the order (duplicate or late event) → no-op.</summary>
    public async Task OnOrderPlacedAsync(Guid orderId, Guid fulfillmentLocationId, Guid deliverySlotId, DateTimeOffset placedAt, CancellationToken cancellationToken)
    {
        if (await shipments.GetByOrderAsync(orderId, cancellationToken) is not null)
        {
            return;
        }

        shipments.Add(Shipment.Create(orderId, fulfillmentLocationId, deliverySlotId, placedAt));
    }

    /// <summary>
    /// <c>OrderCancelled</c>: PENDING / ASSIGNED → CANCELLED + slot released; out for delivery → <c>ShipmentRecallRequired</c>;
    /// CANCELLED → no-op; no shipment yet → a CANCELLED shipment is created and the slot released.
    /// </summary>
    public Task OnOrderCancelledAsync(Guid orderId, Guid fulfillmentLocationId, Guid deliverySlotId, DateTimeOffset cancelledAt, CancellationToken cancellationToken)
        => CancelAsync(orderId, fulfillmentLocationId, deliverySlotId, cancelledAt, recallIfDispatched: true, cancellationToken);

    /// <summary><c>OrderPaymentFailed</c>: as cancel, but an unpaid order is never dispatched (no recall).</summary>
    public Task OnOrderPaymentFailedAsync(Guid orderId, Guid fulfillmentLocationId, Guid deliverySlotId, DateTimeOffset failedAt, CancellationToken cancellationToken)
        => CancelAsync(orderId, fulfillmentLocationId, deliverySlotId, failedAt, recallIfDispatched: false, cancellationToken);

    /// <summary>
    /// <c>OrderPacked</c>: sets <c>packed_at</c> and cold box. No shipment yet (events out of order) → throws so the outbox
    /// retries after <c>OrderPlaced</c> is handled.
    /// </summary>
    public async Task OnOrderPackedAsync(Guid orderId, DateTimeOffset packedAt, bool coldBoxUsed, CancellationToken cancellationToken)
    {
        var shipment = await shipments.GetByOrderAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"No shipment for order {orderId} yet; the event is retried.");
        shipment.MarkPacked(packedAt, coldBoxUsed);
    }

    private async Task CancelAsync(Guid orderId, Guid fulfillmentLocationId, Guid deliverySlotId, DateTimeOffset at, bool recallIfDispatched, CancellationToken cancellationToken)
    {
        var shipment = await shipments.GetByOrderAsync(orderId, cancellationToken);
        if (shipment is null)
        {
            shipments.Add(Shipment.CreateCancelled(orderId, fulfillmentLocationId, deliverySlotId, at));
            await ReleaseSlotAsync(deliverySlotId, cancellationToken);
            return;
        }

        if (shipment.Cancel(at))
        {
            await ReleaseSlotAsync(shipment.DeliverySlotId, cancellationToken);
        }
        else if (recallIfDispatched)
        {
            shipment.RequestRecall();
        }
    }

    private async Task ReleaseSlotAsync(Guid deliverySlotId, CancellationToken cancellationToken)
    {
        var slot = await slots.GetAsync(deliverySlotId, cancellationToken);
        slot?.Release();
    }
}
