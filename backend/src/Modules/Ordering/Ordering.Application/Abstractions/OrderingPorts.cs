using FreshOrbit.Ordering.Application.Complaints;
using FreshOrbit.Ordering.Application.Orders;
using FreshOrbit.Ordering.Domain.Complaints;
using FreshOrbit.Ordering.Domain.Invoices;
using FreshOrbit.Ordering.Domain.Orders;

namespace FreshOrbit.Ordering.Application.Abstractions;

/// <summary>Orders on the shared unit of work (tracked, with lines, history, adjustments and substitutions).</summary>
public interface IOrderRepository
{
    Task<Order?> GetAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Order?> FindByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken);

    Task<bool> HasQualifyingOrderAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Ids of PENDING_PAYMENT orders whose <c>payment_due_at</c> has passed (partial index), oldest first.</summary>
    Task<IReadOnlyList<Guid>> ListUnpaidDueAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);

    /// <summary>(order id, substitution id) of PROPOSED substitutions past <c>expires_at</c>, oldest first.</summary>
    Task<IReadOnlyList<(Guid OrderId, Guid SubstitutionId)>> ListExpiredSubstitutionsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);

    void Add(Order order);
}

public interface IComplaintRepository
{
    Task<Complaint?> GetAsync(Guid complaintId, CancellationToken cancellationToken);

    /// <summary>True when an OPEN or APPROVED complaint exists for the line (or for the order when the line is null).</summary>
    Task<bool> HasOpenAsync(Guid orderId, Guid? orderItemId, CancellationToken cancellationToken);

    void Add(Complaint complaint);

    /// <summary>Makes the save update the complaint row (row version checked and bumped) when only a child changed (photo added).</summary>
    void MarkChanged(Complaint complaint);
}

public interface IInvoiceRepository
{
    Task<Invoice?> FindByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    void Add(Invoice invoice);
}

/// <summary>
/// Runs a command in its own explicit transaction and repeats it from Read after a lost race
/// (<c>ConcurrentSaveException</c>, unique violation, deadlock): 3 retries after the first attempt, then
/// <c>CONCURRENT_SAVE</c> (ORD-R13, Q14). The change tracker is cleared before every retry.
/// </summary>
public interface IOrderingTransactions
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> attempt, CancellationToken cancellationToken);
}

/// <summary>Releases the stock reservation of one order line (substitution accept, ORD-R35); adapter over Inventory.Public.</summary>
public interface IOrderStockReleaser
{
    Task ReleaseAsync(Guid orderId, Guid orderItemId, CancellationToken cancellationToken);
}

/// <summary>Payment methods enabled for a city (G7): the city's order policy ∩ what Payments can take.</summary>
public interface IPaymentMethodAvailability
{
    Task<IReadOnlySet<PaymentMethod>> GetEnabledMethodsAsync(Guid cityId, CancellationToken cancellationToken);
}

/// <summary>Shipment facts for tracking (Delivery <c>GetShipmentForOrder</c>, G6).</summary>
public interface IShipmentTracking
{
    Task<ShipmentTrackingInfo?> GetShipmentForOrderAsync(Guid orderId, CancellationToken cancellationToken);
}

public sealed record ShipmentTrackingInfo(
    Guid ShipmentId,
    string Status,
    string? RiderName,
    string? RiderPhone,
    DateTimeOffset? DispatchedAt,
    bool ColdBoxUsed,
    string? DeliveryCode,
    IReadOnlyList<ShipmentAttemptInfo> Attempts);

public sealed record ShipmentAttemptInfo(int AttemptNo, string Outcome, DateTimeOffset AttemptedAt);

/// <summary>Ordering settings that are not business policy (policy values come from Settings' order policy).</summary>
public sealed class OrderingOptions
{
    public const string SectionName = "Ordering";

    /// <summary>How far ahead checkout looks for the chosen slot (days).</summary>
    public int SlotLookaheadDays { get; set; } = 14;

    /// <summary>SAC code printed on the delivery-fee invoice line.</summary>
    public string DeliveryServiceCode { get; set; } = "996813";
}

/// <summary>Read-side queries (lists and the pick list) projected straight from the tables.</summary>
public interface IOrderReads
{
    Task<(IReadOnlyList<OrderListItem> Items, int Total)> ListForCustomerAsync(Guid customerId, OrderStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    Task<(IReadOnlyList<AdminOrderListItem> Items, int Total)> ListForStaffAsync(AdminOrderFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// ORD-R60: ACTIVE lines of CONFIRMED/PREPARING orders of the slot, sum of gross per variant x preparation (one
    /// query), and the number of distinct orders those lines belong to (one count query, G18).
    /// </summary>
    Task<PickListData> PickListAsync(Guid fulfillmentLocationId, Guid slotId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ComplaintResponse>> ListComplaintsForOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ComplaintResponse> Items, int Total)> ListComplaintsAsync(ComplaintStatus? status, ComplaintKind? kind, Guid? fulfillmentLocationId, IReadOnlySet<Guid>? locationIds, int page, int pageSize, CancellationToken cancellationToken);
}
