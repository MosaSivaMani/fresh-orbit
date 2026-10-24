using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Ordering.Domain.Orders;

/// <summary>
/// An order (<c>ordering.orders</c>, row version <c>xmin</c>) with its lines, status history, adjustments and
/// substitutions. Decides the placement snapshot, the status machine (docs/api §6), cancellation, weigh-and-pack
/// final amounts, short supply, substitutions and adjustments (docs/product/ordering.md).
/// Every mutation sets <see cref="ChangedAt"/>, so the root row (and its <c>xmin</c>) changes with any child row.
/// </summary>
public sealed partial class Order : AggregateRoot
{
    public const int MaxCustomerNotesLength = 500;

    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.PendingPayment] = [OrderStatus.Confirmed, OrderStatus.PaymentFailed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Preparing, OrderStatus.Cancelled],
        [OrderStatus.Preparing] = [OrderStatus.Packed, OrderStatus.Cancelled],
        [OrderStatus.Packed] = [OrderStatus.OutForDelivery, OrderStatus.Cancelled],
        [OrderStatus.OutForDelivery] = [OrderStatus.Delivered, OrderStatus.DeliveryFailed],
        [OrderStatus.DeliveryFailed] = [OrderStatus.OutForDelivery, OrderStatus.Returned],
    };

    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusHistory> _history = [];
    private readonly List<OrderAdjustment> _adjustments = [];
    private readonly List<Substitution> _substitutions = [];

    private Order()
    {
        OrderNumber = IdempotencyKey = RecipientName = Phone = Line1 = Locality = CityName = StateCode = Pincode = string.Empty;
    }

    private Order(OrderPlacement placement)
        : base(placement.OrderId)
    {
        OrderNumber = placement.OrderNumber;
        CustomerId = placement.CustomerId;
        CartId = placement.CartId;
        CityId = placement.CityId;
        FulfillmentLocationId = placement.FulfillmentLocationId;
        ServiceAreaId = placement.ServiceAreaId;
        TimeZone = placement.TimeZone;
        PaymentMethod = placement.PaymentMethod;
        SubstitutionPreference = placement.SubstitutionPreference;
        var address = placement.Address;
        AddressId = address.AddressId;
        RecipientName = address.RecipientName;
        Phone = address.Phone;
        Line1 = address.Line1;
        Line2 = address.Line2;
        Landmark = address.Landmark;
        Locality = address.Locality;
        CityName = address.CityName;
        StateCode = address.StateCode;
        Pincode = address.Pincode;
        Latitude = address.Latitude;
        Longitude = address.Longitude;
        DeliverySlotId = placement.DeliverySlotId;
        SlotStartAt = placement.SlotStartAt;
        SlotEndAt = placement.SlotEndAt;
        CustomerNotes = string.IsNullOrWhiteSpace(placement.CustomerNotes) ? null : placement.CustomerNotes.Trim();
        IdempotencyKey = placement.IdempotencyKey;
        CouponCode = placement.CouponCode;
        var amounts = placement.Amounts;
        ItemsPaise = amounts.ItemsPaise;
        PreparationPaise = amounts.PreparationPaise;
        DiscountPaise = amounts.DiscountPaise;
        DeliveryFeePaise = amounts.DeliveryFeePaise;
        DeliveryFeeTaxRateBp = amounts.DeliveryFeeTaxRateBp;
        TaxPaise = amounts.TaxPaise;
        TotalPaise = amounts.TotalPaise;
        MaxTotalPaise = amounts.MaxTotalPaise;
        AmountDuePaise = amounts.TotalPaise;
        PlacedAt = placement.PlacedAt;
        ChangedAt = placement.PlacedAt;
        PaymentDueAt = placement.PaymentMethod == PaymentMethod.Online ? placement.PaymentDueAt : null;
    }

    public string OrderNumber { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid CartId { get; private set; }

    public Guid CityId { get; private set; }

    public Guid FulfillmentLocationId { get; private set; }

    public Guid ServiceAreaId { get; private set; }

    /// <summary>IANA time zone of the fulfillment location (order and invoice number periods).</summary>
    public string? TimeZone { get; private set; }

    public OrderStatus Status { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    public SubstitutionPreference SubstitutionPreference { get; private set; }

    public Guid AddressId { get; private set; }

    public string RecipientName { get; private set; }

    public string Phone { get; private set; }

    public string Line1 { get; private set; }

    public string? Line2 { get; private set; }

    public string? Landmark { get; private set; }

    public string Locality { get; private set; }

    public string CityName { get; private set; }

    public string StateCode { get; private set; }

    public string Pincode { get; private set; }

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public Guid DeliverySlotId { get; private set; }

    public DateTimeOffset SlotStartAt { get; private set; }

    public DateTimeOffset SlotEndAt { get; private set; }

    public string? CustomerNotes { get; private set; }

    public string IdempotencyKey { get; private set; }

    public string? CouponCode { get; private set; }

    public long ItemsPaise { get; private set; }

    public long PreparationPaise { get; private set; }

    public long DiscountPaise { get; private set; }

    public long DeliveryFeePaise { get; private set; }

    public int DeliveryFeeTaxRateBp { get; private set; }

    public long TaxPaise { get; private set; }

    /// <summary>Estimated total at placement.</summary>
    public long TotalPaise { get; private set; }

    public long MaxTotalPaise { get; private set; }

    public long? FinalItemsPaise { get; private set; }

    public long? FinalPreparationPaise { get; private set; }

    public long? FinalDiscountPaise { get; private set; }

    public long? FinalTaxPaise { get; private set; }

    public long? FinalTotalPaise { get; private set; }

    /// <summary>What the customer still owes at the door (COD / UPI on delivery); 0 for prepaid orders after pack.</summary>
    public long AmountDuePaise { get; private set; }

    /// <summary>What Payments must refund (prepaid shortfall, goodwill after collection).</summary>
    public long RefundDuePaise { get; private set; }

    public long RefundDueWeightShortfallPaise { get; private set; }

    public long RefundDueShortSupplyPaise { get; private set; }

    public bool ColdBoxUsed { get; private set; }

    /// <summary>The captured online payment that confirmed the order.</summary>
    public Guid? PaymentId { get; private set; }

    public long CapturedPaise { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    public DateTimeOffset? PaymentDueAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public DateTimeOffset? PreparingAt { get; private set; }

    public DateTimeOffset? PackedAt { get; private set; }

    public DateTimeOffset? DispatchedAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? CancelReasonCode { get; private set; }

    public Guid? ShipmentId { get; private set; }

    /// <summary>Last change to the order or one of its rows; keeps the root's row version moving with its children.</summary>
    public DateTimeOffset ChangedAt { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public IReadOnlyList<OrderStatusHistory> History => _history;

    public IReadOnlyList<OrderAdjustment> Adjustments => _adjustments;

    public IReadOnlyList<Substitution> Substitutions => _substitutions;

    public bool IsTerminal => Status is OrderStatus.Delivered or OrderStatus.Cancelled or OrderStatus.PaymentFailed or OrderStatus.Returned;

    public bool IsPrepaid => PaymentMethod == PaymentMethod.Online;

    /// <summary>Creates the order from a checkout decision (ORD-R14, R16–R19); raises OrderPlaced and OrderStatusChanged.</summary>
    public static Order Place(OrderPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (placement.Items.Count == 0)
        {
            throw new DomainException(OrderingErrors.CartEmpty, "An order needs at least one line.");
        }

        // W10: order times are shown in the location's zone, so an order never exists without it (rule 7).
        if (string.IsNullOrWhiteSpace(placement.TimeZone))
        {
            throw new DomainException(OrderingErrors.ConfigurationMissing, "The delivery location has no time zone; choose another delivery address.", "deliveryAddressId");
        }

        if (placement.PaymentMethod == PaymentMethod.Online && placement.PaymentDueAt is null)
        {
            throw new ArgumentException("An ONLINE order needs a payment due time.", nameof(placement));
        }

        var order = new Order(placement);
        var lineNo = 0;
        foreach (var snapshot in placement.Items)
        {
            order._items.Add(new OrderItem(order.Id, ++lineNo, snapshot, null));
        }

        var initial = placement.PaymentMethod == PaymentMethod.Online ? OrderStatus.PendingPayment : OrderStatus.Confirmed;
        order.Status = initial;
        if (initial == OrderStatus.Confirmed)
        {
            order.ConfirmedAt = placement.PlacedAt;
        }

        var actor = OrderActor.Customer(placement.CustomerId);
        order._history.Add(new OrderStatusHistory(order.Id, null, initial, actor, null, placement.PlacedAt));
        order.Raise(new OrderPlaced(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            order.FulfillmentLocationId,
            order.CartId,
            order.CityId,
            order.ServiceAreaId,
            order.DeliverySlotId,
            order.SlotStartAt,
            order.SlotEndAt,
            initial.ToCode(),
            order.PaymentMethod.ToCode(),
            order.PaymentDueAt,
            order.AmountDuePaise,
            order.TotalPaise,
            order.MaxTotalPaise,
            order.DiscountPaise,
            order.DeliveryFeePaise,
            order.CouponCode,
            [.. order._items.Select(i => new OrderPlacedLine(i.Id, i.VariantId, i.PreparationCode, i.StockUnit, i.YieldBp, i.OrderedQuantity, i.GrossQuantity))],
            order.PlacedAt));
        order.RaiseStatusChanged(null, initial, actor, null, placement.PlacedAt);
        return order;
    }

    /// <summary>True when §2 allows <paramref name="from"/> → <paramref name="to"/>.</summary>
    public static bool CanMove(OrderStatus from, OrderStatus to)
        => Transitions.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Position in PENDING_PAYMENT &lt; CONFIRMED &lt; PREPARING &lt; PACKED &lt; OUT_FOR_DELIVERY; others rank after.</summary>
    public static int Rank(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => 0,
        OrderStatus.Confirmed => 1,
        OrderStatus.Preparing => 2,
        OrderStatus.Packed => 3,
        OrderStatus.OutForDelivery => 4,
        _ => 5,
    };

    public OrderItem? FindItem(Guid orderItemId) => _items.Find(i => i.Id == orderItemId);

    /// <summary>CONFIRMED → PREPARING (staff <c>orders.pack</c>).</summary>
    public void StartPreparing(OrderActor actor, DateTimeOffset at) => ChangeStatus(OrderStatus.Preparing, actor, null, at);

    /// <summary>
    /// Customer cancel (ORD-R38): PENDING_PAYMENT always; CONFIRMED / PREPARING while status ≤ policy
    /// <paramref name="cancelAllowedUntil"/>; PACKED and later → ORDER_NOT_CANCELLABLE; closed → INVALID_STATUS_TRANSITION.
    /// </summary>
    public void CancelByCustomer(CustomerCancelReason reason, OrderStatus cancelAllowedUntil, DateTimeOffset at)
    {
        if (Status is OrderStatus.Cancelled or OrderStatus.PaymentFailed)
        {
            throw InvalidTransition(OrderStatus.Cancelled);
        }

        var allowed = Status == OrderStatus.PendingPayment
            || (Status is OrderStatus.Confirmed or OrderStatus.Preparing && Rank(Status) <= Rank(cancelAllowedUntil));
        if (!allowed)
        {
            throw new DomainException(OrderingErrors.OrderNotCancellable, "This order can no longer be cancelled; contact support.");
        }

        Cancel(OrderActor.Customer(CustomerId), reason.ToCode(), at);
    }

    /// <summary>Staff cancel (ORD-R39): CONFIRMED, PREPARING, PACKED; later → 422; PENDING_PAYMENT or closed → 409.</summary>
    public void CancelByStaff(StaffCancelReason reason, Guid? staffMemberId, DateTimeOffset at)
    {
        if (Status is OrderStatus.PendingPayment or OrderStatus.Cancelled or OrderStatus.PaymentFailed)
        {
            throw InvalidTransition(OrderStatus.Cancelled);
        }

        if (Status is not (OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Packed))
        {
            throw new DomainException(OrderingErrors.OrderNotCancellable, "Orders out for delivery or later cannot be cancelled.");
        }

        Cancel(OrderActor.Staff(staffMemberId), reason.ToCode(), at);
    }

    /// <summary>
    /// <c>PaymentCaptured</c> (ORD-R43): PENDING_PAYMENT → CONFIRMED; the same payment again → no-op; a closed order
    /// or one paid by another attempt → PaymentReceivedForClosedOrder.
    /// </summary>
    public PaymentCaptureOutcome ConfirmPayment(Guid paymentId, long amountPaise, DateTimeOffset at)
    {
        if (Status == OrderStatus.PendingPayment)
        {
            PaymentId = paymentId;
            CapturedPaise = amountPaise;
            ChangeStatus(OrderStatus.Confirmed, OrderActor.System, null, at);
            Raise(new OrderConfirmed(Id, OrderNumber, CustomerId, FulfillmentLocationId, paymentId, at));
            return PaymentCaptureOutcome.Confirmed;
        }

        if (PaymentId == paymentId)
        {
            return PaymentCaptureOutcome.AlreadyConfirmed;
        }

        Touch(at);
        Raise(new PaymentReceivedForClosedOrder(Id, OrderNumber, CustomerId, FulfillmentLocationId, paymentId, Status.ToCode(), amountPaise));
        return PaymentCaptureOutcome.ClosedOrderPayment;
    }

    /// <summary>
    /// <c>PaymentFailed</c> (ORD-R44): only when PENDING_PAYMENT, no other active attempt and the payment window has
    /// passed; otherwise no change (the customer may retry until due).
    /// </summary>
    public bool FailPayment(bool hasOtherActiveAttempt, DateTimeOffset now)
    {
        if (Status != OrderStatus.PendingPayment || hasOtherActiveAttempt || PaymentDueAt is not { } due || now < due)
        {
            return false;
        }

        MarkPaymentFailed(PaymentFailureReason.PaymentFailed, now);
        return true;
    }

    /// <summary>Expiry job (ORD-R45): PENDING_PAYMENT past <see cref="PaymentDueAt"/> → PAYMENT_FAILED (PAYMENT_TIMEOUT).</summary>
    public bool ExpireIfUnpaid(DateTimeOffset now)
    {
        if (Status != OrderStatus.PendingPayment || PaymentDueAt is not { } due || now < due)
        {
            return false;
        }

        MarkPaymentFailed(PaymentFailureReason.PaymentTimeout, now);
        return true;
    }

    /// <summary><c>ShipmentDispatched</c>: PACKED → OUT_FOR_DELIVERY (ORD-R46).</summary>
    public DeliveryEventOutcome Dispatch(Guid shipmentId, Guid? riderId, DateTimeOffset at)
    {
        var outcome = Check(OrderStatus.OutForDelivery);
        if (outcome != DeliveryEventOutcome.Applied)
        {
            return outcome;
        }

        ShipmentId = shipmentId;
        ChangeStatus(OrderStatus.OutForDelivery, OrderActor.System, null, at);
        Raise(new OrderDispatched(Id, OrderNumber, CustomerId, FulfillmentLocationId, shipmentId, riderId, at));
        return outcome;
    }

    /// <summary>Whether <c>ShipmentDelivered</c> applies now (invoice is issued only then).</summary>
    public DeliveryEventOutcome CheckDeliver() => Check(OrderStatus.Delivered);

    /// <summary><c>ShipmentDelivered</c>: OUT_FOR_DELIVERY → DELIVERED with the invoice issued in the same transaction (ORD-R48).</summary>
    public DeliveryEventOutcome Deliver(Guid shipmentId, Guid invoiceId, string invoiceNumber, DateTimeOffset at)
    {
        var outcome = Check(OrderStatus.Delivered);
        if (outcome != DeliveryEventOutcome.Applied)
        {
            return outcome;
        }

        ShipmentId = shipmentId;
        ChangeStatus(OrderStatus.Delivered, OrderActor.System, null, at);
        Raise(new OrderDelivered(Id, OrderNumber, CustomerId, FulfillmentLocationId, shipmentId, at, FinalTotalPaise ?? TotalPaise, AmountDuePaise, invoiceId, invoiceNumber));
        return outcome;
    }

    /// <summary><c>DeliveryAttemptFailed</c>: OUT_FOR_DELIVERY → DELIVERY_FAILED.</summary>
    public DeliveryEventOutcome FailDeliveryAttempt(string? outcomeCode, DateTimeOffset at)
    {
        var outcome = Check(OrderStatus.DeliveryFailed);
        if (outcome == DeliveryEventOutcome.Applied)
        {
            ChangeStatus(OrderStatus.DeliveryFailed, OrderActor.System, outcomeCode, at);
        }

        return outcome;
    }

    /// <summary><c>ShipmentReattempted</c>: DELIVERY_FAILED → OUT_FOR_DELIVERY.</summary>
    public DeliveryEventOutcome Reattempt(DateTimeOffset at)
    {
        if (Status == OrderStatus.OutForDelivery)
        {
            return DeliveryEventOutcome.AlreadyApplied;
        }

        if (Status != OrderStatus.DeliveryFailed)
        {
            return DeliveryEventOutcome.Rejected;
        }

        ChangeStatus(OrderStatus.OutForDelivery, OrderActor.System, null, at);
        return DeliveryEventOutcome.Applied;
    }

    /// <summary><c>ShipmentReturned</c>: DELIVERY_FAILED → RETURNED.</summary>
    public DeliveryEventOutcome Return(Guid shipmentId, DateTimeOffset at)
    {
        var outcome = Check(OrderStatus.Returned);
        if (outcome != DeliveryEventOutcome.Applied)
        {
            return outcome;
        }

        ShipmentId = shipmentId;
        ChangeStatus(OrderStatus.Returned, OrderActor.System, null, at);
        Raise(new OrderReturned(Id, OrderNumber, CustomerId, FulfillmentLocationId, shipmentId, PaymentMethod.ToCode(), at));
        return outcome;
    }

    private static ConflictException InvalidTransition(OrderStatus to)
        => new(OrderingErrors.InvalidStatusTransition, $"The order cannot move to {to.ToCode()} from its current status.");

    private DeliveryEventOutcome Check(OrderStatus target)
        => Status == target ? DeliveryEventOutcome.AlreadyApplied : CanMove(Status, target) ? DeliveryEventOutcome.Applied : DeliveryEventOutcome.Rejected;

    private void Cancel(OrderActor actor, string reasonCode, DateTimeOffset at)
    {
        var previous = Status;
        CloseOpenWork(at, actor.Type);
        CancelReasonCode = reasonCode;
        AmountDuePaise = 0;
        ChangeStatus(OrderStatus.Cancelled, actor, reasonCode, at);
        Raise(new OrderCancelled(
            Id,
            OrderNumber,
            CustomerId,
            FulfillmentLocationId,
            previous.ToCode(),
            DeliverySlotId,
            actor.Type.ToCode(),
            actor.Id,
            reasonCode,
            PaymentMethod.ToCode(),
            previous == OrderStatus.Packed,
            CouponCode,
            at));
    }

    private void MarkPaymentFailed(PaymentFailureReason reason, DateTimeOffset at)
    {
        CloseOpenWork(at, ActorType.System);
        AmountDuePaise = 0;
        ChangeStatus(OrderStatus.PaymentFailed, OrderActor.System, reason.ToCode(), at);
        Raise(new OrderPaymentFailed(Id, OrderNumber, CustomerId, FulfillmentLocationId, DeliverySlotId, reason.ToCode(), CouponCode, at));
    }

    /// <summary>ACTIVE lines → CANCELLED and PROPOSED substitutions → EXPIRED (ORD-R41, R45).</summary>
    private void CloseOpenWork(DateTimeOffset at, ActorType actorType)
    {
        foreach (var item in _items.Where(i => i.Status == OrderItemStatus.Active))
        {
            item.MarkStatus(OrderItemStatus.Cancelled);
        }

        foreach (var substitution in _substitutions.Where(s => s.Status == SubstitutionStatus.Proposed))
        {
            substitution.Resolve(SubstitutionStatus.Expired, actorType, null, null, at);
        }
    }

    /// <summary>ORD-R20: validates §2, sets the status timestamp, appends history and raises OrderStatusChanged.</summary>
    private void ChangeStatus(OrderStatus to, OrderActor actor, string? reasonCode, DateTimeOffset at)
    {
        if (!CanMove(Status, to))
        {
            throw InvalidTransition(to);
        }

        var from = Status;
        Status = to;
        switch (to)
        {
            case OrderStatus.Confirmed:
                ConfirmedAt = at;
                break;
            case OrderStatus.Preparing:
                PreparingAt = at;
                break;
            case OrderStatus.Packed:
                PackedAt = at;
                break;
            case OrderStatus.OutForDelivery:
                DispatchedAt = at;
                break;
            case OrderStatus.Delivered:
                DeliveredAt = at;
                ClosedAt = at;
                break;
            case OrderStatus.Cancelled:
                CancelledAt = at;
                ClosedAt = at;
                break;
            case OrderStatus.PaymentFailed:
            case OrderStatus.Returned:
                ClosedAt = at;
                break;
        }

        _history.Add(new OrderStatusHistory(Id, from, to, actor, reasonCode, at));
        Touch(at);
        RaiseStatusChanged(from, to, actor, reasonCode, at);
    }

    private void RaiseStatusChanged(OrderStatus? from, OrderStatus to, OrderActor actor, string? reasonCode, DateTimeOffset at)
        => Raise(new OrderStatusChanged(Id, OrderNumber, CustomerId, FulfillmentLocationId, from?.ToCode(), to.ToCode(), actor.Type.ToCode(), actor.Id, reasonCode, at));

    private void Touch(DateTimeOffset at) => ChangedAt = at > ChangedAt ? at : ChangedAt.AddTicks(10);
}

/// <summary>Result of applying <c>PaymentCaptured</c>.</summary>
public enum PaymentCaptureOutcome
{
    Confirmed,
    AlreadyConfirmed,
    ClosedOrderPayment,
}

/// <summary>Result of applying a Delivery event (ORD-R46).</summary>
public enum DeliveryEventOutcome
{
    Applied,
    AlreadyApplied,

    /// <summary>The transition is not allowed (e.g. dispatch after cancel): no change, warning logged.</summary>
    Rejected,
}
