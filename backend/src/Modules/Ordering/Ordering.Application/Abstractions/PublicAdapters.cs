using FreshOrbit.Delivery.Public;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Payments.Public;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.Codes;
using DomainPaymentMethod = FreshOrbit.Ordering.Domain.Orders.PaymentMethod;

namespace FreshOrbit.Ordering.Application.Abstractions;

/// <summary><see cref="IOrderStockReleaser"/> over <c>IInventoryApi.ReleaseAsync</c> (tracked; saved by the caller).</summary>
public sealed class InventoryOrderStockReleaser(IInventoryApi inventory) : IOrderStockReleaser
{
    public Task ReleaseAsync(Guid orderId, Guid orderItemId, CancellationToken cancellationToken)
        => inventory.ReleaseAsync([orderItemId], cancellationToken);
}

/// <summary>Methods of the city's order policy that Payments can take right now (G7).</summary>
public sealed class PolicyPaymentMethodAvailability(ISettingsApi settings, IPaymentsApi payments) : IPaymentMethodAvailability
{
    public async Task<IReadOnlySet<DomainPaymentMethod>> GetEnabledMethodsAsync(Guid cityId, CancellationToken cancellationToken)
    {
        var policy = await settings.GetOrderPolicyAsync(cityId, cancellationToken);
        var available = (await payments.GetAvailablePaymentMethodsAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var methods = new HashSet<DomainPaymentMethod>();
        foreach (var code in policy.EnabledPaymentMethods.Where(available.Contains))
        {
            if (EnumCodes.TryToEnum<DomainPaymentMethod>(code, out var method))
            {
                methods.Add(method);
            }
        }

        return methods;
    }
}

/// <summary><see cref="IShipmentTracking"/> over <c>IDeliveryApi.GetShipmentForOrderAsync</c>.</summary>
public sealed class DeliveryShipmentTracking(IDeliveryApi delivery) : IShipmentTracking
{
    public async Task<ShipmentTrackingInfo?> GetShipmentForOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var shipment = await delivery.GetShipmentForOrderAsync(orderId, cancellationToken);
        return shipment is null
            ? null
            : new ShipmentTrackingInfo(
                shipment.ShipmentId,
                shipment.Status,
                shipment.RiderName,
                shipment.RiderPhone,
                shipment.DispatchedAt,
                shipment.ColdBoxUsed,
                shipment.DeliveryCode,
                [.. shipment.Attempts.Select(a => new ShipmentAttemptInfo(a.AttemptNo, a.Outcome, a.AttemptedAt))]);
    }
}
