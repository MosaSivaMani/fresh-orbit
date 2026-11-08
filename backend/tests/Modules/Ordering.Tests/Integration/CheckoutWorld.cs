using FreshOrbit.Cart.Public;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Customers.Public;
using FreshOrbit.Delivery.Public;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Payments.Public;
using FreshOrbit.Pricing.Public;
using FreshOrbit.Promotions.Public;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Pricing;
using FreshOrbit.SharedKernel.Units;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using CatalogSellUnit = FreshOrbit.Catalog.Public.SellUnit;
using CatalogStockUnit = FreshOrbit.Catalog.Public.StockUnit;

namespace FreshOrbit.Ordering.Tests.Integration;

/// <summary>
/// The other modules as seen through their Public APIs: NSubstitute doubles plus small fakes for the cart (which can
/// force a lost race) and Settings (a row-locked number series in the caller's transaction).
/// </summary>
public sealed class CheckoutWorld
{
    public static readonly Guid LocationId = Guid.CreateVersion7();
    public static readonly Guid CityId = Guid.CreateVersion7();
    public static readonly Guid ServiceAreaId = Guid.CreateVersion7();
    public const string Pincode = "600041";
    public const string TimeZone = "Asia/Kolkata";

    public CheckoutWorld(DateTimeOffset now)
    {
        Now = now;
        SlotId = Guid.CreateVersion7();
        AddressId = Guid.CreateVersion7();
        CartId = Guid.CreateVersion7();
        Lines = [Fish(500), Crabs(2)];

        Customers.GetCustomerAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new CustomerSummary(ci.Arg<Guid>(), Guid.NewGuid(), "Priya Raman", "+919876543210", null, CustomerStatus.Active));
        Customers.GetDeliveryAddressAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Guid>(1) == AddressId
                ? new DeliveryAddress(AddressId, "Priya Raman", "+919876543210", "12, 3rd Cross Street", null, "Near RTO", "Thiruvanmiyur", "Chennai", "TN", Pincode, null, null) { Label = "HOME" }
                : null);
        Locations.ResolvePincodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => new Serviceability(true, ci.Arg<string>(), CityId, "CHN", TimeZone, LocationId, ServiceAreaId, 0, 50000, 20000));
        Locations.GetFulfillmentLocationsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new FulfillmentLocationSummary(LocationId, CityId, "CHN-TVM", "Thiruvanmiyur", FulfillmentLocationStatus.Active, TimeZone)]);
        Delivery.GetOpenSlotsAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ => [new SlotInfo(SlotId, Now.AddHours(5), Now.AddHours(7), Now.AddHours(3), SlotRemaining)]);
        Delivery.BookSlotAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new SlotBooking(true, ci.ArgAt<Guid>(0), Now.AddHours(5), Now.AddHours(7), null));
        Inventory.ReserveAsync(Arg.Any<ReserveStockRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ReserveStockResult(ReserveSucceeds, ReserveSucceeds ? [] : [Lines[0].VariantId]));
        Inventory.GetOrderStockAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => Stock(ci.Arg<Guid>()));
        Payments.GetAvailablePaymentMethodsAsync(Arg.Any<CancellationToken>())
            .Returns(["COD", "UPI_ON_DELIVERY", "ONLINE"]);
        Payments.GetPaymentStatusAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new OrderPaymentStatus(ci.Arg<Guid>(), PaidPaise > 0 ? PaymentStatus.Captured : PaymentStatus.Pending, PaidPaise, 0));
        Promotions.RedeemAsync(Arg.Any<RedeemCouponRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var request = ci.Arg<RedeemCouponRequest>();
                var parts = Money.Allocate(CouponDiscountPaise, [.. request.Lines.Select(l => l.AmountPaise)]);
                return new CouponRedemptionResult(Guid.NewGuid(), Guid.NewGuid(), request.Code, CouponDiscountPaise, false, [.. request.Lines.Select((l, i) => new CouponLineDiscount(l.LineRef, parts[i]))]);
            });
        Catalog.GetVariantsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ci => [.. ci.Arg<IReadOnlyCollection<Guid>>().Where(id => id == SubstituteVariantId).Select(_ => SubstituteVariant())]);
        Pricing.GetLinePricesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<PriceKey>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci => [.. ci.Arg<IReadOnlyCollection<PriceKey>>().Select(k => new LinePrice(k.VariantId, k.PreparationMethodId, 89900, null, 0, null, 0))]);
    }

    public DateTimeOffset Now { get; set; }

    public Guid SlotId { get; }

    public Guid AddressId { get; }

    public Guid CartId { get; set; }

    public Guid SubstituteVariantId { get; } = Guid.CreateVersion7();

    public int SlotRemaining { get; set; } = 5;

    /// <summary>What Inventory reports for an order (admin order detail); empty by default.</summary>
    public Func<Guid, OrderStock> Stock { get; set; } = orderId => new OrderStock(orderId, [], []);

    public bool ReserveSucceeds { get; set; } = true;

    public long PaidPaise { get; set; }

    public long CouponDiscountPaise { get; set; }

    public string? CouponCode { get; set; }

    /// <summary>Lines of the server cart (price facts as the cart would quote them now).</summary>
    public List<QuoteLine> Lines { get; set; }

    /// <summary>How many MarkConverted calls lose a race against a concurrent writer before one wins.</summary>
    public int RacesToLose { get; set; }

    public int MarkConvertedCalls { get; set; }

    public IReadOnlyList<CartLineToAdd>? ReorderedLines { get; set; }

    public Guid ProbeId { get; } = Guid.CreateVersion7();

    public ICustomersApi Customers { get; } = Substitute.For<ICustomersApi>();

    public ILocationsApi Locations { get; } = Substitute.For<ILocationsApi>();

    public IDeliveryApi Delivery { get; } = Substitute.For<IDeliveryApi>();

    public IInventoryApi Inventory { get; } = Substitute.For<IInventoryApi>();

    public IPaymentsApi Payments { get; } = Substitute.For<IPaymentsApi>();

    public IPromotionsApi Promotions { get; } = Substitute.For<IPromotionsApi>();

    public ICatalogApi Catalog { get; } = Substitute.For<ICatalogApi>();

    public IPricingApi Pricing { get; } = Substitute.For<IPricingApi>();

    public OrderPolicy Policy { get; set; } = new(15, 1000, "CONFIRMED", 500000)
    {
        EnabledPaymentMethods = ["COD", "UPI_ON_DELIVERY", "ONLINE"],
        SubstitutionResponseMinutes = 15,
        ComplaintWindowHours = 24,
        MaxDeliveryAttempts = 3,
    };

    public BusinessProfile Profile { get; set; } = new("Fresh Orbit Foods Pvt Ltd", "Fresh Orbit", "33ABCDE1234F1Z5", null, "Chennai", "+914400000000", "care@example.com")
    {
        TimeZone = "Asia/Kolkata",
    };

    public static QuoteLine Fish(int grams) => Quote("KG", "GRAM", 129900, 4000, "PER_KG", grams, true, 8000, "Seer Fish", "Medium", "STEAKS", "Steaks");

    public static QuoteLine Crabs(int pieces) => Quote("PIECE", "PIECE", 65000, 0, null, pieces, false, 10000, "Mud Crab", "Live", null, null);

    public static QuoteLine Quote(string sellUnit, string stockUnit, long price, long prepRate, string? basis, int quantity, bool weightVariable, int yieldBp, string product, string variant, string? prepCode, string? prepName)
    {
        var facts = new LinePriceFacts(sellUnit == "KG" ? PricedUnit.Gram : PricedUnit.Piece, price, prepRate, basis == "PER_KG" ? PreparationChargeBasis.PerKg : null, 0);
        var amounts = LinePricing.Compute(facts, quantity);
        var max = LinePricing.MaxChargeableQuantity(quantity, weightVariable, 1000);
        var maxAmounts = LinePricing.Compute(facts, max);
        return new QuoteLine(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            product,
            Guid.CreateVersion7(),
            variant,
            "SKU",
            null,
            "0302",
            sellUnit,
            stockUnit,
            prepCode is null ? null : Guid.CreateVersion7(),
            prepCode,
            prepName,
            yieldBp,
            weightVariable,
            quantity,
            max,
            stockUnit == "GRAM" ? Weight.GrossForNet(quantity, yieldBp) : quantity,
            price,
            prepRate,
            basis,
            0,
            amounts.ItemPaise,
            amounts.PreparationPaise,
            amounts.TotalPaise,
            maxAmounts.TotalPaise,
            0,
            0,
            true,
            []);
    }

    public long EstimatedTotalPaise => Money.Sum(Lines.Select(l => l.EstimatedTotalPaise)) - CouponDiscountPaise;

    public CheckoutCart CheckoutCart() => new(CartId, LocationId, Pincode, CouponCode, [.. Lines.Select(l => new CheckoutCartLine(l.LineId, l.VariantId, l.PreparationMethodId, l.Quantity))]);

    public CartQuote BuildQuote()
    {
        var discounts = CouponDiscountPaise > 0 ? Money.Allocate(CouponDiscountPaise, [.. Lines.Select(l => l.EstimatedTotalPaise)]) : [.. Lines.Select(_ => 0L)];
        var lines = Lines.Select((l, i) => l with { DiscountPaise = discounts[i] }).ToList();
        var items = Money.Sum(lines.Select(l => l.ItemPaise));
        var prep = Money.Sum(lines.Select(l => l.PreparationPaise));
        var max = Money.Sum(lines.Select(l => l.MaxLineTotalPaise)) - CouponDiscountPaise;
        var totals = new CartTotalsView(items, prep, items + prep, CouponDiscountPaise, 0, true, 0, items + prep - CouponDiscountPaise, max, 20000, 0, null);
        var coupon = CouponCode is null ? null : new CartCouponView(CouponCode, "APPLIED", null, CouponDiscountPaise, null);
        var view = new CartView(CartId, null, Pincode, "CHN-TVM", [], coupon, totals, true, [], []);
        return new CartQuote(CartId, Pincode, LocationId, "CHN-TVM", CityId, ServiceAreaId, TimeZone, true, 1000, 0, CouponCode, view, lines);
    }

    public VariantInfo SubstituteVariant() => new(
        SubstituteVariantId,
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        "Tiger Prawns",
        "tiger-prawns",
        "Large",
        "PRW-L",
        null,
        "0306",
        "SEAFOOD",
        CatalogSellUnit.Kg,
        CatalogStockUnit.Gram,
        null,
        null,
        250,
        5000,
        250,
        true,
        true,
        []);

    /// <summary>Simulates a concurrent writer committing on another connection (bumps the probe's row version).</summary>
    public async Task BumpProbeElsewhereAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE ordering_test.probes SET touches = touches + 1 WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", ProbeId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task EnsureProbeAsync(FreshOrbitDbContext db)
    {
        if (!await db.Set<Probe>().AnyAsync(p => p.Id == ProbeId))
        {
            db.Add(new Probe(ProbeId));
            await db.SaveChangesAsync();
        }
    }
}
