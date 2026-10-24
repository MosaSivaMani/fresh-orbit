using FreshOrbit.Customers.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Ordering.Domain.Complaints;
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
    private static readonly DomainStatus[] OpenStatuses =
        [DomainStatus.PendingPayment, DomainStatus.Confirmed, DomainStatus.Preparing, DomainStatus.Packed, DomainStatus.OutForDelivery];

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

    public async Task<IReadOnlyDictionary<Guid, string>> GetOrderNumbersAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderIds);
        if (orderIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = orderIds.Distinct().ToList();
        return await db.Set<Order>().AsNoTracking().Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNumber, cancellationToken);
    }

    public async Task<CustomerOrderPage> ListCustomerOrdersAsync(Guid customerId, IReadOnlyCollection<Guid>? fulfillmentLocationIds, int skip, int take, CancellationToken cancellationToken = default)
    {
        var orders = CustomerOrders([customerId], fulfillmentLocationIds);
        var total = await orders.CountAsync(cancellationToken);
        var rows = await orders
            .OrderByDescending(o => o.PlacedAt).ThenByDescending(o => o.Id)
            .Skip(skip)
            .Take(take)
            .Select(o => new { o.Id, o.OrderNumber, o.Status, o.PaymentMethod, o.FulfillmentLocationId, o.PlacedAt, o.SlotStartAt, o.SlotEndAt, o.TotalPaise, o.FinalTotalPaise })
            .ToListAsync(cancellationToken);
        return new CustomerOrderPage(
            [.. rows.Select(o => new CustomerOrderItem(o.Id, o.OrderNumber, ToPublic(o.Status), ToPublic(o.PaymentMethod), o.FulfillmentLocationId, o.PlacedAt, o.SlotStartAt, o.SlotEndAt, o.TotalPaise, o.FinalTotalPaise))],
            total);
    }

    public async Task<IReadOnlyDictionary<Guid, CustomerOrderCounts>> GetCustomerOrderCountsAsync(IReadOnlyCollection<Guid> customerIds, IReadOnlyCollection<Guid>? fulfillmentLocationIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customerIds);
        var open = OpenStatuses;
        var rows = await CustomerOrders(customerIds, fulfillmentLocationIds)
            .Where(o => o.Status != DomainStatus.PaymentFailed)
            .GroupBy(o => o.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Total = g.Count(),
                Open = g.Count(o => open.Contains(o.Status)),
                Delivered = g.Count(o => o.Status == DomainStatus.Delivered),
                Cancelled = g.Count(o => o.Status == DomainStatus.Cancelled),
                LastPlacedAt = g.Max(o => (DateTimeOffset?)o.PlacedAt),
            })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.CustomerId, r => new CustomerOrderCounts(r.Total, r.Open, r.Delivered, r.Cancelled, r.LastPlacedAt));
    }

    private IQueryable<Order> CustomerOrders(IReadOnlyCollection<Guid> customerIds, IReadOnlyCollection<Guid>? fulfillmentLocationIds)
    {
        var customers = customerIds.Distinct().ToList();
        var orders = db.Set<Order>().AsNoTracking().Where(o => customers.Contains(o.CustomerId));
        if (fulfillmentLocationIds is not null)
        {
            var locations = fulfillmentLocationIds.Distinct().ToList();
            orders = orders.Where(o => locations.Contains(o.FulfillmentLocationId));
        }

        return orders;
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
/// A customer account cannot be deleted while the customer has an active (non-terminal) order or an OPEN/APPROVED
/// complaint (Customers' <c>ICustomerDeletionCheck</c>).
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
        var openComplaint = await db.Set<Complaint>().AnyAsync(
            c => c.CustomerId == customerId && (c.Status == ComplaintStatus.Open || c.Status == ComplaintStatus.Approved),
            cancellationToken);
        var items = new List<string>();
        if (active)
        {
            items.Add(ActiveOrdersExist);
        }

        if (openComplaint)
        {
            items.Add(CustomerOpenMatters.OpenComplaint);
        }

        return items;
    }
}



