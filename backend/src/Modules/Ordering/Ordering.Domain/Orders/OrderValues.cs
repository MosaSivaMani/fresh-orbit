using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Pricing;

namespace FreshOrbit.Ordering.Domain.Orders;

/// <summary>Who changed an order (history and events).</summary>
public sealed record OrderActor(ActorType Type, Guid? Id)
{
    public static OrderActor System { get; } = new(ActorType.System, null);

    public static OrderActor Customer(Guid customerId) => new(ActorType.Customer, customerId);

    public static OrderActor Staff(Guid? staffMemberId) => new(ActorType.Staff, staffMemberId);
}

/// <summary>Delivery address copied onto the order at placement (ORD-R16).</summary>
public sealed record AddressSnapshot(
    Guid AddressId,
    string RecipientName,
    string Phone,
    string Line1,
    string? Line2,
    string? Landmark,
    string Locality,
    string CityName,
    string StateCode,
    string Pincode,
    decimal? Latitude,
    decimal? Longitude);

/// <summary>Estimated amounts of the order at placement (API §5.2).</summary>
public sealed record OrderAmounts(
    long ItemsPaise,
    long PreparationPaise,
    long DiscountPaise,
    long DeliveryFeePaise,
    int DeliveryFeeTaxRateBp,
    long TaxPaise,
    long TotalPaise,
    long MaxTotalPaise);

/// <summary>Frozen line facts at placement or substitution accept (ORD-R17).</summary>
public sealed record OrderItemSnapshot(
    Guid ItemId,
    Guid ProductId,
    Guid CategoryId,
    Guid VariantId,
    string ProductName,
    string VariantName,
    string Sku,
    string? ImageKey,
    string HsnCode,
    Guid? PreparationMethodId,
    string? PreparationCode,
    string? PreparationName,
    string SellUnit,
    string StockUnit,
    long UnitPricePaise,
    long PreparationChargeRatePaise,
    string? ChargeBasis,
    int TaxRateBp,
    int YieldBp,
    bool IsWeightVariable,
    int Quantity,
    int GrossQuantity,
    int MaxChargeableQuantity,
    long ItemPaise,
    long PreparationPaise,
    long EstimatedTotalPaise,
    long MaxLineTotalPaise,
    long DiscountPaise,
    long TaxPaise)
{
    /// <summary>Quantity unit of the line in requests and responses: KG → GRAM, PACK → PACK, else PIECE.</summary>
    public string QuantityUnit => OrderUnits.QuantityUnitOf(SellUnit);
}

/// <summary>Everything the checkout decided, turned into an order by <see cref="Order.Place"/>.</summary>
public sealed record OrderPlacement(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid CartId,
    Guid CityId,
    Guid FulfillmentLocationId,
    Guid ServiceAreaId,
    string? TimeZone,
    PaymentMethod PaymentMethod,
    SubstitutionPreference SubstitutionPreference,
    AddressSnapshot Address,
    Guid DeliverySlotId,
    DateTimeOffset SlotStartAt,
    DateTimeOffset SlotEndAt,
    string? CustomerNotes,
    string IdempotencyKey,
    string? CouponCode,
    OrderAmounts Amounts,
    IReadOnlyList<OrderItemSnapshot> Items,
    DateTimeOffset PlacedAt,
    DateTimeOffset? PaymentDueAt);

/// <summary>One line of a pack request.</summary>
public sealed record PackLine(Guid OrderItemId, int PackedQuantity, string Unit);

/// <summary>Per-line pack outcome (API §4.15 pack response).</summary>
public sealed record PackedLineResult(
    Guid OrderItemId,
    int PackedQuantity,
    int ChargedQuantity,
    string Unit,
    bool Capped,
    long FinalTotalPaise,
    long AdjustmentPaise,
    OrderItemStatus Status);

/// <summary>Unit helpers shared by placement, pack and substitution.</summary>
public static class OrderUnits
{
    public const string Kg = "KG";
    public const string Gram = "GRAM";
    public const string Piece = "PIECE";
    public const string Pack = "PACK";

    public static string QuantityUnitOf(string sellUnit) => sellUnit switch
    {
        Kg => Gram,
        Pack => Pack,
        _ => Piece,
    };

    public static PricedUnit PricedUnitOf(string sellUnit) => sellUnit == Kg ? PricedUnit.Gram : PricedUnit.Piece;

    public static PreparationChargeBasis? BasisOf(string? chargeBasis) => chargeBasis switch
    {
        "PER_KG" => PreparationChargeBasis.PerKg,
        "PER_ITEM" => PreparationChargeBasis.PerItem,
        "FLAT" => PreparationChargeBasis.Flat,
        _ => null,
    };
}
