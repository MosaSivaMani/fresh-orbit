using FreshOrbit.Locations.Public;
using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainPaymentMethod = FreshOrbit.Ordering.Domain.Orders.PaymentMethod;
using DomainStatus = FreshOrbit.Ordering.Domain.Orders.OrderStatus;
using PublicPaymentMethod = FreshOrbit.Ordering.Public.PaymentMethod;
using PublicStatus = FreshOrbit.Ordering.Public.OrderStatus;

namespace FreshOrbit.Ordering.Infrastructure.Public;

/// <summary>Implementation of <see cref="IOrderingApi"/> (read-only, no tracking).</summary>
internal sealed class OrderingApi(FreshOrbitDbContext db) : IOrderingApi
{
    public async Task<OrderSummary?> GetOrderSummaryAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.Set<Order>().AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        return order is null
            ? null
            : new OrderSummary(order.Id, order.OrderNumber, order.CustomerId, ToPublic(order.Status), ToPublic(order.PaymentMethod), order.TotalPaise, order.FinalTotalPaise, order.AmountDuePaise, order.FulfillmentLocationId);
    }

    public Task<bool> HasQualifyingOrderAsync(Guid customerId, CancellationToken cancellationToken = default)
        => db.Set<Order>().AnyAsync(o => o.CustomerId == customerId && o.Status != DomainStatus.Cancelled && o.Status != DomainStatus.PaymentFailed, cancellationToken);

    public async Task<OrderPaymentFacts?> GetOrderPaymentFactsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.Set<Order>().AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        return order is null
            ? null
            : new OrderPaymentFacts(order.Id, order.CustomerId, order.OrderNumber, ToPublic(order.Status), ToPublic(order.PaymentMethod), order.AmountDuePaise, order.PaymentDueAt, order.FulfillmentLocationId);
    }

    public async Task<PublicStatus?> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var status = await db.Set<Order>().AsNoTracking().Where(o => o.Id == orderId).Select(o => (DomainStatus?)o.Status).FirstOrDefaultAsync(cancellationToken);
        return status is { } s ? ToPublic(s) : null;
    }

    private static PublicStatus ToPublic(DomainStatus status) => status.ToCode().ToEnum<PublicStatus>();

    private static PublicPaymentMethod ToPublic(DomainPaymentMethod method) => method.ToCode().ToEnum<PublicPaymentMethod>();
}

/// <summary>A city cannot be retired while it has open (non-terminal) orders.</summary>
internal sealed class OrderingCityRetirementCheck(FreshOrbitDbContext db) : ICityRetirementCheck
{
    public const string OpenOrders = "ORDERS_OPEN";

    public async Task<IReadOnlyList<string>> GetBlockingItemsAsync(Guid cityId, CancellationToken cancellationToken = default)
    {
        var open = await db.Set<Order>().AnyAsync(
            o => o.CityId == cityId
                && o.Status != DomainStatus.Delivered
                && o.Status != DomainStatus.Cancelled
                && o.Status != DomainStatus.PaymentFailed
                && o.Status != DomainStatus.Returned,
            cancellationToken);
        return open ? [OpenOrders] : [];
    }
}

/// <summary>
/// A customer account cannot be deleted while the customer has an active (non-terminal) order. Implements
/// Customers' <c>ICustomerDeletionCheck</c> (same signature); that interface is not on this branch yet, so at merge
/// add <c>: ICustomerDeletionCheck</c> and register it with <c>TryAddEnumerable</c>.
/// </summary>
public sealed class OrderingCustomerDeletionCheck(FreshOrbitDbContext db)
{
    public const string ActiveOrdersExist = "ACTIVE_ORDERS_EXIST";

    public async Task<IReadOnlyList<string>> GetBlockingItemsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var active = await db.Set<Order>().AnyAsync(
            o => o.CustomerId == customerId
                && o.Status != DomainStatus.Delivered
                && o.Status != DomainStatus.Cancelled
                && o.Status != DomainStatus.PaymentFailed
                && o.Status != DomainStatus.Returned,
            cancellationToken);
        return active ? [ActiveOrdersExist] : [];
    }
}

/// <summary>
/// Merge gap: Inventory.Public has no <c>Release</c>. The SUBSTITUTED line's reservation is then released by
/// Inventory's <c>OrderPacked</c> / <c>OrderCancelled</c> handlers instead of in the accept transaction.
/// </summary>
internal sealed partial class InventoryGapStockReleaser(ILogger<InventoryGapStockReleaser> logger) : IOrderStockReleaser
{
    public Task ReleaseAsync(Guid orderId, Guid orderItemId, CancellationToken cancellationToken)
    {
        LogReleaseDeferred(orderId, orderItemId);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reservation of order {OrderId} line {OrderItemId} is released by Inventory on pack/cancel (no Release in Inventory.Public yet)")]
    private partial void LogReleaseDeferred(Guid orderId, Guid orderItemId);
}

/// <summary>Merge gap (G7): enabled payment methods from configuration until Payments.Public exposes them per city.</summary>
internal sealed class ConfiguredPaymentMethodAvailability(OrderingOptions options) : IPaymentMethodAvailability
{
    public Task<IReadOnlySet<DomainPaymentMethod>> GetEnabledMethodsAsync(Guid cityId, CancellationToken cancellationToken)
    {
        var methods = new HashSet<DomainPaymentMethod>();
        foreach (var code in options.EnabledPaymentMethods)
        {
            if (EnumCodes.TryToEnum<DomainPaymentMethod>(code, out var method))
            {
                methods.Add(method);
            }
        }

        return Task.FromResult<IReadOnlySet<DomainPaymentMethod>>(methods);
    }
}

/// <summary>Merge gap (G6): Delivery.Public has no <c>GetShipmentForOrder</c> yet; tracking shows no shipment.</summary>
internal sealed class DeliveryGapShipmentTracking : IShipmentTracking
{
    public Task<ShipmentTrackingInfo?> GetShipmentForOrderAsync(Guid orderId, CancellationToken cancellationToken)
        => Task.FromResult<ShipmentTrackingInfo?>(null);
}
