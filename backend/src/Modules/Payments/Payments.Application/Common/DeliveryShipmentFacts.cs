using FreshOrbit.Delivery.Public;

namespace FreshOrbit.Payments.Application.Common;

/// <summary><see cref="IShipmentFacts"/> over <c>Delivery.Public</c>.</summary>
public sealed class DeliveryShipmentFacts(IDeliveryApi delivery) : IShipmentFacts
{
    public async Task<ShipmentFacts?> GetShipmentAsync(Guid shipmentId, CancellationToken cancellationToken)
        => await delivery.GetShipmentAsync(shipmentId, cancellationToken) is { } shipment
            ? new ShipmentFacts(shipment.ShipmentId, shipment.OrderId, shipment.RiderId, shipment.FulfillmentLocationId)
            : null;

    public async Task<RiderFacts?> GetRiderAsync(Guid riderId, CancellationToken cancellationToken)
        => await delivery.GetRiderAsync(riderId, cancellationToken) is { } rider
            ? new RiderFacts(rider.RiderId, rider.FulfillmentLocationId, rider.TimeZone, rider.IsActive)
            : null;
}
