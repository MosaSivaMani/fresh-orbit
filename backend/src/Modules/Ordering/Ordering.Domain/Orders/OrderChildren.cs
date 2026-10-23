using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Pricing;

namespace FreshOrbit.Ordering.Domain.Orders;

/// <summary>
/// One order line (<c>ordering.order_items</c>): the frozen placement snapshot (ORD-R17) plus the final amounts
/// written at weigh-and-pack (ORD-R28, D16).
/// </summary>
public sealed class OrderItem : Entity
{
    private OrderItem()
    {
        ProductName = VariantName = Sku = HsnCode = SellUnit = StockUnit = QuantityUnit = string.Empty;
    }

    internal OrderItem(Guid orderId, int lineNo, OrderItemSnapshot snapshot, Guid? substitutedFromItemId)
        : base(snapshot.ItemId)
    {
        OrderId = orderId;
        LineNo = lineNo;
        ProductId = snapshot.ProductId;
        CategoryId = snapshot.CategoryId;
        VariantId = snapshot.VariantId;
        ProductName = snapshot.ProductName;
        VariantName = snapshot.VariantName;
        Sku = snapshot.Sku;
        ImageKey = snapshot.ImageKey;
        HsnCode = snapshot.HsnCode;
        PreparationMethodId = snapshot.PreparationMethodId;
        PreparationCode = snapshot.PreparationCode;
        PreparationName = snapshot.PreparationName;
        SellUnit = snapshot.SellUnit;
        StockUnit = snapshot.StockUnit;
        QuantityUnit = snapshot.QuantityUnit;
        UnitPricePaise = snapshot.UnitPricePaise;
        PreparationChargePaise = snapshot.PreparationChargeRatePaise;
        ChargeBasis = snapshot.ChargeBasis;
        TaxRateBp = snapshot.TaxRateBp;
        YieldBp = snapshot.YieldBp;
        IsWeightVariable = snapshot.IsWeightVariable;
        OrderedQuantity = snapshot.Quantity;
        GrossQuantity = snapshot.GrossQuantity;
        MaxChargeableQuantity = snapshot.MaxChargeableQuantity;
        EstimatedItemPaise = snapshot.ItemPaise;
        EstimatedPreparationPaise = snapshot.PreparationPaise;
        EstimatedTotalPaise = snapshot.EstimatedTotalPaise;
        MaxLineTotalPaise = snapshot.MaxLineTotalPaise;
        DiscountPaise = snapshot.DiscountPaise;
        EstimatedTaxPaise = snapshot.TaxPaise;
        SubstitutedFromItemId = substitutedFromItemId;
        Status = OrderItemStatus.Active;
    }

    public Guid OrderId { get; private set; }

    public int LineNo { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid CategoryId { get; private set; }

    public Guid VariantId { get; private set; }

    public string ProductName { get; private set; }

    public string VariantName { get; private set; }

    public string Sku { get; private set; }

    public string? ImageKey { get; private set; }

    public string HsnCode { get; private set; }

    public Guid? PreparationMethodId { get; private set; }

    public string? PreparationCode { get; private set; }

    public string? PreparationName { get; private set; }

    /// <summary>KG / PIECE / PACK.</summary>
    public string SellUnit { get; private set; }

    /// <summary>GRAM / PIECE (stock and event unit).</summary>
    public string StockUnit { get; private set; }

    /// <summary>GRAM / PIECE / PACK (request and response unit).</summary>
    public string QuantityUnit { get; private set; }

    public long UnitPricePaise { get; private set; }

    /// <summary>Preparation charge rate in the unit of <see cref="ChargeBasis"/> (G10).</summary>
    public long PreparationChargePaise { get; private set; }

    public string? ChargeBasis { get; private set; }

    public int TaxRateBp { get; private set; }

    public int YieldBp { get; private set; }

    public bool IsWeightVariable { get; private set; }

    public int OrderedQuantity { get; private set; }

    public int GrossQuantity { get; private set; }

    public int MaxChargeableQuantity { get; private set; }

    public long EstimatedItemPaise { get; private set; }

    public long EstimatedPreparationPaise { get; private set; }

    public long EstimatedTotalPaise { get; private set; }

    public long MaxLineTotalPaise { get; private set; }

    /// <summary>Coupon discount allocated to the line at placement (PROMO-R14).</summary>
    public long DiscountPaise { get; private set; }

    public long EstimatedTaxPaise { get; private set; }

    public int? PackedQuantity { get; private set; }

    public int? ChargedQuantity { get; private set; }

    public long? FinalItemPaise { get; private set; }

    public long? FinalPreparationPaise { get; private set; }

    /// <summary>Final discount on the line (coupon re-allocated at pack plus any prepaid cap waiver).</summary>
    public long? FinalDiscountPaise { get; private set; }

    public long? FinalTaxPaise { get; private set; }

    /// <summary>Final line total before discount (item + preparation at the charged quantity).</summary>
    public long? FinalTotalPaise { get; private set; }

    public OrderItemStatus Status { get; private set; }

    public Guid? SubstitutedFromItemId { get; private set; }

    /// <summary>Product – variant – preparation, as printed on the invoice.</summary>
    public string Description => PreparationName is { Length: > 0 } preparation
        ? $"{ProductName} – {VariantName} – {preparation}"
        : $"{ProductName} – {VariantName}";

    internal LinePriceFacts Facts => new(OrderUnits.PricedUnitOf(SellUnit), UnitPricePaise, PreparationChargePaise, OrderUnits.BasisOf(ChargeBasis), TaxRateBp);

    internal void SetFinal(int packed, int charged, LineAmounts amounts)
    {
        PackedQuantity = packed;
        ChargedQuantity = charged;
        FinalItemPaise = amounts.ItemPaise;
        FinalPreparationPaise = amounts.PreparationPaise;
        FinalTotalPaise = amounts.TotalPaise;
        FinalDiscountPaise = 0;
        FinalTaxPaise = 0;
    }

    internal void SetFinalDiscountAndTax(long discountPaise)
    {
        FinalDiscountPaise = discountPaise;
        FinalTaxPaise = LinePricing.ContainedTax((FinalTotalPaise ?? 0) - discountPaise, TaxRateBp);
    }

    internal void SetZeroFinal()
    {
        PackedQuantity ??= 0;
        ChargedQuantity = 0;
        FinalItemPaise = 0;
        FinalPreparationPaise = 0;
        FinalTotalPaise = 0;
        FinalDiscountPaise = 0;
        FinalTaxPaise = 0;
    }

    internal void MarkStatus(OrderItemStatus status) => Status = status;
}

/// <summary>One status transition (<c>ordering.order_status_history</c>).</summary>
public sealed class OrderStatusHistory : Entity
{
    private OrderStatusHistory()
    {
    }

    internal OrderStatusHistory(Guid orderId, OrderStatus? fromStatus, OrderStatus toStatus, OrderActor actor, string? reasonCode, DateTimeOffset at)
    {
        OrderId = orderId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorType = actor.Type;
        ActorId = actor.Id;
        ReasonCode = reasonCode;
        At = at;
    }

    public Guid OrderId { get; private set; }

    public OrderStatus? FromStatus { get; private set; }

    public OrderStatus ToStatus { get; private set; }

    public ActorType ActorType { get; private set; }

    public Guid? ActorId { get; private set; }

    public string? ReasonCode { get; private set; }

    public DateTimeOffset At { get; private set; }
}

/// <summary>A signed change to what the order costs (<c>ordering.order_adjustments</c>).</summary>
public sealed class OrderAdjustment : Entity
{
    private OrderAdjustment()
    {
        Reason = string.Empty;
    }

    internal OrderAdjustment(Guid orderId, Guid? orderItemId, AdjustmentKind kind, long amountPaise, string reason, Guid? createdBy, DateTimeOffset at)
    {
        OrderId = orderId;
        OrderItemId = orderItemId;
        Kind = kind;
        AmountPaise = amountPaise;
        Reason = reason;
        CreatedByActorId = createdBy;
        At = at;
    }

    public Guid OrderId { get; private set; }

    public Guid? OrderItemId { get; private set; }

    public AdjustmentKind Kind { get; private set; }

    public long AmountPaise { get; private set; }

    public string Reason { get; private set; }

    public Guid? CreatedByActorId { get; private set; }

    public DateTimeOffset At { get; private set; }
}

/// <summary>A proposed replacement for a line (<c>ordering.substitutions</c>).</summary>
public sealed class Substitution : Entity
{
    private Substitution()
    {
        ProposedName = StockUnit = QuantityUnit = string.Empty;
    }

    internal Substitution(Guid orderId, OrderItem original, OrderItemSnapshot proposed, string proposedName, DateTimeOffset proposedAt, DateTimeOffset expiresAt)
    {
        OrderId = orderId;
        OrderItemId = original.Id;
        ProposedVariantId = proposed.VariantId;
        ProposedPreparationMethodId = proposed.PreparationMethodId;
        ProposedPreparationCode = proposed.PreparationCode;
        ProposedName = proposedName;
        ProposedQuantity = proposed.Quantity;
        QuantityUnit = proposed.QuantityUnit;
        StockUnit = proposed.StockUnit;
        PriceDifferencePaise = proposed.EstimatedTotalPaise - original.EstimatedTotalPaise;
        ProposedMaxTotalPaise = proposed.MaxLineTotalPaise;
        Status = SubstitutionStatus.Proposed;
        ProposedAt = proposedAt;
        ExpiresAt = expiresAt;
    }

    public Guid OrderId { get; private set; }

    public Guid OrderItemId { get; private set; }

    public Guid ProposedVariantId { get; private set; }

    public Guid? ProposedPreparationMethodId { get; private set; }

    public string? ProposedPreparationCode { get; private set; }

    public string ProposedName { get; private set; }

    public int ProposedQuantity { get; private set; }

    public string QuantityUnit { get; private set; }

    public string StockUnit { get; private set; }

    /// <summary>Proposed estimated line total − original estimated line total (ORD-R33).</summary>
    public long PriceDifferencePaise { get; private set; }

    public long ProposedMaxTotalPaise { get; private set; }

    public SubstitutionStatus Status { get; private set; }

    public DateTimeOffset ProposedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public ActorType? ResponseActorType { get; private set; }

    public Guid? NewOrderItemId { get; private set; }

    internal void Resolve(SubstitutionStatus status, ActorType actorType, Guid? newOrderItemId, long? priceDifferencePaise, DateTimeOffset at)
    {
        Status = status;
        ResponseActorType = actorType;
        NewOrderItemId = newOrderItemId;
        RespondedAt = at;
        if (priceDifferencePaise is { } difference)
        {
            PriceDifferencePaise = difference;
        }
    }
}
