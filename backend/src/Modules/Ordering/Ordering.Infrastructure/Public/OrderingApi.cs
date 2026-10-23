using FreshOrbit.Customers.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
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
/// A customer account cannot be deleted while the customer has an active (non-terminal) order (Customers'
/// <c>ICustomerDeletionCheck</c>).
/// </summary>
public sealed class OrderingCustomerDeletionCheck(FreshOrbitDbContext db) : ICustomerDeletionCheck
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



