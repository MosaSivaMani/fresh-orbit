using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Pricing;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Ordering.Tests;

/// <summary>Builds orders and line snapshots for tests with the shared line formula.</summary>
public static class OrderFactory
{
    public static readonly DateTimeOffset PlacedAt = new(2026, 10, 23, 5, 41, 12, TimeSpan.Zero);

    /// <summary>Seer fish 500 g at ₹1,299/kg + ₹40/kg steaks (API §4.15 line 1).</summary>
    public static OrderItemSnapshot SeerFish(long discountPaise = 0) => Line("KG", 129900, 4000, "PER_KG", 500, true, 0, discountPaise, "Seer Fish", "Medium", "Steaks");

    /// <summary>Tiger prawns 500 g at ₹899/kg + ₹60/kg peeling (line 2).</summary>
    public static OrderItemSnapshot Prawns(long discountPaise = 0) => Line("KG", 89900, 6000, "PER_KG", 500, true, 0, discountPaise, "Tiger Prawns", "Large", "Peeled");

    /// <summary>Two mud crabs at ₹650 each (line 3).</summary>
    public static OrderItemSnapshot Crabs(long discountPaise = 0) => Line("PIECE", 65000, 0, null, 2, false, 0, discountPaise, "Mud Crab", "Live", null);

    public static OrderItemSnapshot Line(
        string sellUnit,
        long unitPricePaise,
        long preparationRatePaise,
        string? chargeBasis,
        int quantity,
        bool isWeightVariable,
        int taxRateBp,
        long discountPaise,
        string productName = "Product",
        string variantName = "Variant",
        string? preparationName = null,
        int maxOverweightBp = 1000,
        Guid? variantId = null)
    {
        var unit = sellUnit == "KG" ? PricedUnit.Gram : PricedUnit.Piece;
        var basis = chargeBasis switch
        {
            "PER_KG" => PreparationChargeBasis.PerKg,
            "PER_ITEM" => PreparationChargeBasis.PerItem,
            "FLAT" => PreparationChargeBasis.Flat,
            _ => (PreparationChargeBasis?)null,
        };
        var facts = new LinePriceFacts(unit, unitPricePaise, preparationRatePaise, basis, taxRateBp);
        var amounts = LinePricing.Compute(facts, quantity);
        var max = LinePricing.MaxChargeableQuantity(quantity, isWeightVariable, maxOverweightBp);
        var maxAmounts = LinePricing.Compute(facts, max);
        var stockUnit = sellUnit == "KG" ? "GRAM" : "PIECE";
        var yieldBp = sellUnit == "KG" ? 8000 : 10000;
        return new OrderItemSnapshot(
            IdGenerator.NewId(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            variantId ?? Guid.CreateVersion7(),
            productName,
            variantName,
            "SKU-1",
            null,
            "0302",
            preparationName is null ? null : Guid.CreateVersion7(),
            preparationName?.ToUpperInvariant(),
            preparationName,
            sellUnit,
            stockUnit,
            unitPricePaise,
            preparationRatePaise,
            chargeBasis,
            taxRateBp,
            yieldBp,
            isWeightVariable,
            quantity,
            stockUnit == "GRAM" ? Weight.GrossForNet(quantity, yieldBp) : quantity,
            max,
            amounts.ItemPaise,
            amounts.PreparationPaise,
            amounts.TotalPaise,
            maxAmounts.TotalPaise,
            discountPaise,
            LinePricing.ContainedTax(amounts.TotalPaise - discountPaise, taxRateBp));
    }

    /// <summary>The API §4.15 example order: three lines, ₹100 coupon allocated over them, free delivery.</summary>
    public static Order ApiExample(PaymentMethod method = PaymentMethod.Cod, SubstitutionPreference preference = SubstitutionPreference.CallMe)
    {
        var parts = Money.Allocate(10000, [66950, 47950, 130000]);
        return Place([SeerFish(parts[0]), Prawns(parts[1]), Crabs(parts[2])], method, preference, discountPaise: 10000);
    }

    public static Order Place(
        IReadOnlyList<OrderItemSnapshot> items,
        PaymentMethod method = PaymentMethod.Cod,
        SubstitutionPreference preference = SubstitutionPreference.CallMe,
        long discountPaise = 0,
        long deliveryFeePaise = 0,
        int deliveryFeeTaxRateBp = 0,
        DateTimeOffset? placedAt = null,
        Guid? customerId = null,
        Guid? fulfillmentLocationId = null,
        string? orderNumber = null,
        string timeZone = "Asia/Kolkata")
    {
        var at = placedAt ?? PlacedAt;
        var itemsPaise = Money.Sum(items.Select(i => i.ItemPaise));
        var preparationPaise = Money.Sum(items.Select(i => i.PreparationPaise));
        var total = itemsPaise + preparationPaise - discountPaise + deliveryFeePaise;
        var maxTotal = Money.Sum(items.Select(i => i.MaxLineTotalPaise)) - discountPaise + deliveryFeePaise;
        var tax = Money.Sum(items.Select(i => i.TaxPaise)) + LinePricing.ContainedTax(deliveryFeePaise, deliveryFeeTaxRateBp);
        return Order.Place(new OrderPlacement(
            IdGenerator.NewId(),
            orderNumber ?? $"FO-{Random.Shared.Next(100000, 999999)}",
            customerId ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            fulfillmentLocationId ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            timeZone,
            method,
            preference,
            new AddressSnapshot(Guid.CreateVersion7(), "Priya Raman", "+919876543210", "12, 3rd Cross Street", null, "Near RTO", "Thiruvanmiyur", "Chennai", "TN", "600041", null, null),
            Guid.CreateVersion7(),
            at.AddHours(3),
            at.AddHours(5),
            "Please call before arriving.",
            Guid.NewGuid().ToString(),
            discountPaise > 0 ? "FRESH100" : null,
            new OrderAmounts(itemsPaise, preparationPaise, discountPaise, deliveryFeePaise, deliveryFeeTaxRateBp, tax, total, maxTotal),
            items,
            at,
            method == PaymentMethod.Online ? at.AddMinutes(15) : null));
    }

    public static OrderActor Staff { get; } = OrderActor.Staff(Guid.CreateVersion7());

    /// <summary>Moves a COD order to PREPARING.</summary>
    public static Order Preparing(this Order order)
    {
        order.StartPreparing(Staff, order.PlacedAt.AddMinutes(30));
        return order;
    }

    /// <summary>Packs every active line at its ordered quantity.</summary>
    public static Order PackedAsOrdered(this Order order, long paidPaise = 0)
    {
        order.Pack([.. order.Items.Where(i => i.Status == OrderItemStatus.Active).Select(i => new PackLine(i.Id, i.OrderedQuantity, i.QuantityUnit))], true, paidPaise, Staff, order.PlacedAt.AddHours(1));
        return order;
    }
}
