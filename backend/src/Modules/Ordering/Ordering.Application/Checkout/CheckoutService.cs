using FreshOrbit.Cart.Public;
using FreshOrbit.Customers.Public;
using FreshOrbit.Delivery.Public;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.Ordering.Application.Orders;
using FreshOrbit.Ordering.Application.Shared;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.Promotions.Public;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Pricing;
using FreshOrbit.SharedKernel.Time;
using DomainPaymentMethod = FreshOrbit.Ordering.Domain.Orders.PaymentMethod;

namespace FreshOrbit.Ordering.Application.Checkout;

/// <summary>
/// Checkout preview and place order (docs/product/ordering.md ORD-R1..R19). Both run the same Read + Decide; place then
/// reserves stock, books the slot, redeems the coupon, takes the order number and converts the cart in one
/// transaction with one save, repeated from Read after a lost race (ORD-R13).
/// </summary>
public sealed class CheckoutService(
    ICustomersApi customers,
    ICartApi carts,
    ILocationsApi locations,
    IDeliveryApi delivery,
    IInventoryApi inventory,
    IPromotionsApi promotions,
    ISettingsApi settings,
    IPaymentMethodAvailability paymentMethods,
    IOrderRepository orders,
    IOrderingTransactions transactions,
    IUnitOfWork unitOfWork,
    OrderQueryService queries,
    OrderingOptions options,
    IClock clock)
{
    public const string OrderSeries = "ORDER";

    private static readonly DomainPaymentMethod[] MethodOrder = [DomainPaymentMethod.Cod, DomainPaymentMethod.UpiOnDelivery, DomainPaymentMethod.Online];

    public async Task<CheckoutPreview> PreviewAsync(Guid customerId, CheckoutRequest request, CancellationToken cancellationToken)
    {
        var decision = await DecideAsync(customerId, request, clock.UtcNow, cancellationToken);
        return decision.Preview;
    }

    /// <summary>Places the order for <paramref name="idempotencyKey"/>; an existing order for the key is returned instead (ORD-R3).</summary>
    public async Task<PlaceOrderOutcome> PlaceAsync(Guid customerId, CheckoutRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var placed = await transactions.RunAsync(ct => PlaceOnceAsync(customerId, request, idempotencyKey, ct), cancellationToken);
        if (placed.Preview is not null)
        {
            return new PlaceOrderOutcome(null, placed.Preview, false);
        }

        var order = await orders.GetAsync(placed.OrderId!.Value, cancellationToken) ?? throw new InvalidOperationException("The placed order was not found.");
        return new PlaceOrderOutcome(await queries.CustomerDetailAsync(order, cancellationToken), null, placed.Replayed);
    }

    private async Task<PlacedResult> PlaceOnceAsync(Guid customerId, CheckoutRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        var existing = await orders.FindByIdempotencyKeyAsync(customerId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return new PlacedResult(existing.Id, null, true);
        }

        var now = clock.UtcNow;
        var decision = await DecideAsync(customerId, request, now, cancellationToken);
        ThrowIfBlocked(decision);
        var quote = decision.Quote!;
        if (request.ExpectedTotalPaise != quote.View.Totals.EstimatedTotalPaise)
        {
            return new PlacedResult(null, decision.Preview, false);
        }

        var orderId = IdGenerator.NewId(now);
        var itemIds = quote.Lines.Select(_ => IdGenerator.NewId(now)).ToList();
        var policy = decision.Policy!;
        var online = decision.Method == DomainPaymentMethod.Online;
        DateTimeOffset? paymentDueAt = online ? now.AddMinutes(policy.ReservationMinutes) : null;

        var reservation = await inventory.ReserveAsync(
            new ReserveStockRequest(orderId, quote.FulfillmentLocationId, [.. quote.Lines.Select((l, i) => new ReserveStockLine(itemIds[i], l.VariantId, l.GrossQuantity))], paymentDueAt),
            cancellationToken);
        if (!reservation.Succeeded)
        {
            var shortIds = reservation.ShortVariantIds.ToHashSet();
            var errors = quote.Lines
                .Select((l, i) => (Line: l, Index: i))
                .Where(x => shortIds.Contains(x.Line.VariantId))
                .Select(x => new FieldError($"lines[{x.Index}]", OrderingErrors.OutOfStock, $"{x.Line.ProductName} is out of stock in the requested quantity."))
                .ToList();
            throw errors.Count > 0
                ? new RefusalException(OrderingErrors.OutOfStock, "Some items are out of stock.", errors)
                : new DomainException(OrderingErrors.OutOfStock, "Some items are out of stock.");
        }

        var booking = await delivery.BookSlotAsync(request.DeliverySlotId, quote.FulfillmentLocationId, cancellationToken);
        if (!booking.Succeeded)
        {
            throw new DomainException(booking.RefusalCode ?? OrderingErrors.SlotNotAvailable, "The delivery slot cannot be booked; choose another slot.", "deliverySlotId");
        }

        var discountByLine = quote.Lines.Select(l => l.DiscountPaise).ToList();
        if (quote.CouponCode is { } couponCode && quote.View.Coupon is { Status: "APPLIED" })
        {
            var redemption = await promotions.RedeemAsync(
                new RedeemCouponRequest(couponCode, customerId, orderId, quote.CityId, await orders.HasQualifyingOrderAsync(customerId, cancellationToken), now, [.. quote.Lines.Select((l, i) => new CouponLine(itemIds[i], l.ProductId, l.CategoryId, l.EstimatedTotalPaise))]),
                cancellationToken);
            if (redemption.DiscountPaise != quote.View.Totals.DiscountPaise)
            {
                // The coupon changed between quote and redeem: lost race, repeat from Read.
                throw new ConcurrentSaveException(null);
            }

            var allocated = redemption.Allocations.ToDictionary(a => a.LineRef, a => a.DiscountPaise);
            discountByLine = [.. itemIds.Select(id => allocated.GetValueOrDefault(id))];
        }

        // Settings owns the series: period (financial year in the business time zone), prefix and gapless counter.
        var orderNumber = await settings.NextNumberAsync(OrderSeries, now, cancellationToken);
        var totals = quote.View.Totals;
        var address = decision.Address!;
        var order = Order.Place(new OrderPlacement(
            orderId,
            orderNumber,
            customerId,
            quote.CartId,
            quote.CityId,
            quote.FulfillmentLocationId,
            quote.ServiceAreaId,
            quote.TimeZone,
            decision.Method,
            decision.Preference,
            new AddressSnapshot(address.AddressId, address.RecipientName, address.Phone, address.Line1, address.Line2, address.Landmark, address.Locality, address.CityName, address.StateCode, address.Pincode, address.Latitude, address.Longitude),
            request.DeliverySlotId,
            booking.StartAt ?? decision.Slot!.StartAt,
            booking.EndAt ?? decision.Slot!.EndAt,
            request.CustomerNotes,
            idempotencyKey,
            quote.View.Coupon is { Status: "APPLIED" } ? quote.CouponCode : null,
            new OrderAmounts(totals.ItemsPaise, totals.PreparationPaise, totals.DiscountPaise, totals.DeliveryFeePaise, quote.DeliveryFeeTaxRateBp, totals.TaxPaise, totals.EstimatedTotalPaise, totals.MaxTotalPaise),
            [.. quote.Lines.Select((l, i) => Snapshot(itemIds[i], l, discountByLine[i]))],
            now,
            paymentDueAt));
        orders.Add(order);
        await carts.MarkConvertedAsync(quote.CartId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PlacedResult(order.Id, null, false);
    }

    /// <summary>Order item snapshot from a quote line (ORD-R17).</summary>
    public static OrderItemSnapshot Snapshot(Guid itemId, QuoteLine line, long discountPaise)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new OrderItemSnapshot(
            itemId,
            line.ProductId,
            line.CategoryId,
            line.VariantId,
            line.ProductName,
            line.VariantName,
            line.Sku,
            line.ImageKey,
            line.HsnCode,
            line.PreparationMethodId,
            line.PreparationCode,
            line.PreparationName,
            line.SellUnit,
            line.StockUnit,
            line.UnitPricePaise,
            line.PreparationChargeRatePaise,
            line.ChargeBasis,
            line.TaxRateBp,
            line.YieldBp,
            line.IsWeightVariable,
            line.Quantity,
            line.GrossQuantity,
            line.MaxChargeableQuantity,
            line.ItemPaise,
            line.PreparationPaise,
            line.EstimatedTotalPaise,
            line.MaxLineTotalPaise,
            discountPaise,
            LinePricing.ContainedTax(line.EstimatedTotalPaise - discountPaise, line.TaxRateBp));
    }

    /// <summary>Refuses the first business problem in rule order (ORD-R4..R9).</summary>
    private static void ThrowIfBlocked(CheckoutDecision decision)
    {
        if (decision.Fatal is { } fatal)
        {
            throw fatal;
        }

        if (!decision.MethodChosen)
        {
            throw new RefusalException(new FieldError("paymentMethod", OrderingErrors.ValidationFailed, "Choose a payment method."));
        }

        var quote = decision.Quote!;
        var lineErrors = quote.Lines
            .Select((l, i) => (Line: l, Index: i))
            .Where(x => !x.Line.IsSellable && x.Line.Issues.Count > 0)
            .Select(x => new FieldError($"lines[{x.Index}]", x.Line.Issues[0].Code, x.Line.Issues[0].Message))
            .ToList();
        if (lineErrors.Count > 0)
        {
            throw new RefusalException(lineErrors[0].Code, "Some items cannot be ordered.", lineErrors);
        }

        var blocker = decision.Preview.Blockers.Count > 0 ? decision.Preview.Blockers[0] : null;
        if (blocker is not null)
        {
            throw blocker.Code == OrderingErrors.CartLocationChanged
                ? new ConflictException(blocker.Code, blocker.Message)
                : new DomainException(blocker.Code, blocker.Message, FieldOf(blocker.Code));
        }
    }

    private static string? FieldOf(string code) => code switch
    {
        OrderingErrors.SlotFull or OrderingErrors.SlotCutoffPassed or OrderingErrors.SlotNotAvailable => "deliverySlotId",
        OrderingErrors.PaymentMethodNotAllowed or OrderingErrors.CodLimitExceeded => "paymentMethod",
        OrderingErrors.AddressNotServiceable => "addressId",
        _ when code.StartsWith("COUPON_", StringComparison.Ordinal) => "couponCode",
        _ => null,
    };

    /// <summary>Read + Decide shared by preview and place (ORD-R1, R4–R9, R15).</summary>
    private async Task<CheckoutDecision> DecideAsync(Guid customerId, CheckoutRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var customer = await customers.GetCustomerAsync(customerId, cancellationToken);
        if (customer is null || customer.Status != CustomerStatus.Active)
        {
            throw OrderingAccess.CustomerDeleted();
        }

        var (method, preference) = ValidateRequest(request);
        var address = await customers.GetDeliveryAddressAsync(customerId, request.AddressId, cancellationToken)
            ?? throw new NotFoundException("The address does not exist.");
        var serviceability = await locations.ResolvePincodeAsync(address.Pincode, cancellationToken);
        var serviceable = serviceability is { IsServiceable: true, FulfillmentLocationId: not null, CityId: not null, ServiceAreaId: not null };
        var addressDto = new CheckoutAddressDto(address.AddressId, address.Label, address.Line1, address.Locality, address.Pincode, serviceable);
        var unknownSlot = new CheckoutSlotDto(request.DeliverySlotId, null, null, null, "NOT_AVAILABLE");

        var cart = await carts.GetCheckoutCartAsync(customerId, cancellationToken);
        if (cart is null || cart.Lines.Count == 0)
        {
            var empty = new CartBlockerView(OrderingErrors.CartEmpty, null, "Your cart is empty.");
            return Blocked(addressDto, unknownSlot, empty, new DomainException(OrderingErrors.CartEmpty, empty.Message));
        }

        if (!serviceable)
        {
            var blocker = new CartBlockerView(OrderingErrors.AddressNotServiceable, null, "We do not deliver to this address yet.");
            return Blocked(addressDto, unknownSlot, blocker, new DomainException(blocker.Code, blocker.Message, "addressId"), cart.CartId);
        }

        var quote = await carts.QuoteAsync(new CartQuoteRequest(customerId, address.Pincode, now), cancellationToken);
        if (quote is null)
        {
            var empty = new CartBlockerView(OrderingErrors.CartEmpty, null, "Your cart is empty.");
            return Blocked(addressDto, unknownSlot, empty, new DomainException(OrderingErrors.CartEmpty, empty.Message));
        }

        var blockers = new List<CartBlockerView>();
        if (cart.FulfillmentLocationId != serviceability.FulfillmentLocationId)
        {
            blockers.Add(new CartBlockerView(OrderingErrors.CartLocationChanged, null, "This address is served from another store; update your cart's delivery location first."));
        }

        blockers.AddRange(quote.View.CheckoutBlockers);

        var (slot, slotBlocker) = await CheckSlotAsync(request.DeliverySlotId, quote.FulfillmentLocationId, now, cancellationToken);
        if (slotBlocker is not null)
        {
            blockers.Add(slotBlocker);
        }

        var policy = await settings.GetOrderPolicyAsync(quote.CityId, cancellationToken);
        var enabled = await paymentMethods.GetEnabledMethodsAsync(quote.CityId, cancellationToken);
        var options = MethodOrder.Select(m => MethodOption(m, enabled, quote.View.Totals.MaxTotalPaise, policy)).ToList();
        if (method is { } chosen && options.First(o => o.Method == chosen.ToCode()) is { Enabled: false } refused)
        {
            blockers.Add(new CartBlockerView(refused.ReasonCode!, null, refused.ReasonCode == OrderingErrors.CodLimitExceeded
                ? "Orders above the cash-on-delivery limit must be paid online."
                : "This payment method is not available here."));
        }

        var slotDto = slot is null ? unknownSlot : new CheckoutSlotDto(slot.SlotId, slot.StartAt, slot.EndAt, slot.CutoffAt, slotBlocker is null ? "AVAILABLE" : SlotStatus(slotBlocker.Code));
        var preview = new CheckoutPreview(
            quote.CartId,
            addressDto,
            quote.FulfillmentLocationCode,
            slotDto,
            quote.View.Lines,
            quote.View.Coupon,
            quote.View.Totals,
            options,
            blockers.Count == 0 && method is not null,
            blockers,
            quote.View.Totals.EstimatedTotalPaise);
        return new CheckoutDecision(preview, null, quote, address, slot, policy, method ?? DomainPaymentMethod.Cod, preference, method is not null);
    }

    private static string SlotStatus(string code) => code switch
    {
        OrderingErrors.SlotFull => "FULL",
        OrderingErrors.SlotCutoffPassed => "CUTOFF_PASSED",
        _ => "NOT_AVAILABLE",
    };

    private static CheckoutDecision Blocked(CheckoutAddressDto address, CheckoutSlotDto slot, CartBlockerView blocker, DomainException fatal, Guid? cartId = null)
    {
        var preview = new CheckoutPreview(cartId, address, null, slot, [], null, CartTotalsView.Empty, [], false, [blocker], 0);
        return new CheckoutDecision(preview, fatal, null, null, null, null, DomainPaymentMethod.Cod, SubstitutionPreference.CallMe, false);
    }

    private static (DomainPaymentMethod? Method, SubstitutionPreference Preference) ValidateRequest(CheckoutRequest request)
    {
        var errors = new List<FieldError>();
        DomainPaymentMethod? method = null;
        if (EnumCodes.TryToEnum<DomainPaymentMethod>(request.PaymentMethod, out var parsedMethod))
        {
            method = parsedMethod;
        }
        else if (request.PaymentMethod is not null)
        {
            errors.Add(new FieldError("paymentMethod", OrderingErrors.ValidationFailed, "Choose COD, UPI_ON_DELIVERY or ONLINE."));
        }

        var preference = SubstitutionPreference.CallMe;
        if (request.SubstitutionPreference is not null && !EnumCodes.TryToEnum(request.SubstitutionPreference, out preference))
        {
            errors.Add(new FieldError("substitutionPreference", OrderingErrors.ValidationFailed, "Choose CALL_ME, AUTO_SIMILAR or NONE."));
        }

        if (request.CustomerNotes is { Length: > Order.MaxCustomerNotesLength })
        {
            errors.Add(new FieldError("customerNotes", OrderingErrors.ValidationFailed, $"Notes can be at most {Order.MaxCustomerNotesLength} characters."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        return (method, preference);
    }

    private static PaymentMethodOption MethodOption(DomainPaymentMethod method, IReadOnlySet<DomainPaymentMethod> enabled, long maxTotalPaise, OrderPolicy policy)
    {
        if (!enabled.Contains(method))
        {
            return new PaymentMethodOption(method.ToCode(), false, OrderingErrors.PaymentMethodNotAllowed);
        }

        return method != DomainPaymentMethod.Online && maxTotalPaise > policy.CodMaxOrderPaise
            ? new PaymentMethodOption(method.ToCode(), false, OrderingErrors.CodLimitExceeded)
            : new PaymentMethodOption(method.ToCode(), true, null);
    }

    private async Task<(SlotInfo? Slot, CartBlockerView? Blocker)> CheckSlotAsync(Guid slotId, Guid locationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Open slots include ones past cutoff for the day so the reason can be named; a slot not listed is not available.
        var slots = await delivery.GetOpenSlotsAsync(locationId, now.AddDays(-1), now.AddDays(options.SlotLookaheadDays), cancellationToken);
        var slot = slots.FirstOrDefault(s => s.SlotId == slotId);
        if (slot is null || slot.EndAt <= now)
        {
            return (slot, new CartBlockerView(OrderingErrors.SlotNotAvailable, null, "The delivery slot is not available; choose another slot."));
        }

        if (now >= slot.CutoffAt)
        {
            return (slot, new CartBlockerView(OrderingErrors.SlotCutoffPassed, null, "Ordering for this slot has closed; choose a later slot."));
        }

        return slot.Remaining <= 0
            ? (slot, new CartBlockerView(OrderingErrors.SlotFull, null, "The delivery slot is full; choose another slot."))
            : (slot, null);
    }

    private sealed record CheckoutDecision(
        CheckoutPreview Preview,
        DomainException? Fatal,
        CartQuote? Quote,
        DeliveryAddress? Address,
        SlotInfo? Slot,
        OrderPolicy? Policy,
        DomainPaymentMethod Method,
        SubstitutionPreference Preference,
        bool MethodChosen);

    private sealed record PlacedResult(Guid? OrderId, CheckoutPreview? Preview, bool Replayed);
}
