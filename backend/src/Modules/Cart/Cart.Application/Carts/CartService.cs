using FreshOrbit.Cart.Application.Abstractions;
using FreshOrbit.Cart.Application.Pricing;
using FreshOrbit.Cart.Domain.Carts;
using FreshOrbit.Cart.Domain.Shared;
using FreshOrbit.Cart.Public;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Pricing.Public;
using FreshOrbit.Promotions.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Cart.Application.Carts;

/// <summary>
/// Cart commands and reads (cart.md §5). Every write runs Read → Decide → Commit inside
/// <see cref="ICartCommandRunner"/> (retried after a lost race) and returns the cart priced after the commit.
/// </summary>
public sealed class CartService(
    ICartRepository carts,
    ICartCommandRunner runner,
    CartPricer pricer,
    ILocationsApi locations,
    IPromotionsApi promotions,
    Ordering.Public.IOrderingApi ordering,
    IClock clock)
{
    public async Task<CartView> GetAsync(CartOwner owner, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var cart = await FindActiveAsync(owner, cancellationToken);
        return cart is null ? EmptyView() : await PriceAsync(cart, cancellationToken);
    }

    public async Task<CartView> AddItemAsync(CartOwner owner, AddCartItemRequest request, string? contextPincode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(request);
        var issuedToken = owner.IsCustomer || owner.GuestToken is not null ? null : GuestTokens.Create();
        var cart = await runner.RunAsync(
            async ct =>
            {
                var cart = await LoadOrCreateAsync(owner, issuedToken, ct);
                var location = await EnsureLocationAsync(cart, contextPincode, ct);
                var check = await CheckLineAsync(location, request.VariantId, request.PreparationCode, request.Quantity, cart, existingItemId: null, ct);
                cart.AddItem(check.Variant.VariantId, check.Preparation?.PreparationMethodId, request.Quantity.Value, check.Rules, clock.UtcNow);
                await carts.SaveChangesAsync(ct);
                return cart;
            },
            cancellationToken);
        var view = await PriceAsync(cart, cancellationToken);
        return issuedToken is null ? view : view with { CartToken = issuedToken };
    }

    public async Task<CartView> ChangeItemAsync(CartOwner owner, Guid itemId, ChangeCartItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cart = await runner.RunAsync(
            async ct =>
            {
                var cart = await RequireActiveAsync(owner, ct);
                var item = cart.FindItem(itemId) ?? throw new NotFoundException(CartErrors.CartItemNotFound, "The cart has no line with this id.");
                var location = await EnsureLocationAsync(cart, null, ct);
                var variants = await pricer.LoadVariantsAsync([item.VariantId], ct);
                var variant = variants.GetValueOrDefault(item.VariantId);
                var preparationCode = request.PreparationCode
                    ?? variant?.Preparations.FirstOrDefault(p => p.PreparationMethodId == item.PreparationMethodId)?.Code;
                var quantity = request.Quantity ?? new QuantityRequest(item.Quantity, variant is null ? "GRAM" : LineCalculator.QuantityUnitCode(variant.SellUnit));
                var check = await CheckLineAsync(location, item.VariantId, preparationCode, quantity, cart, itemId, ct);
                var now = clock.UtcNow;
                if (request.Quantity is not null)
                {
                    cart.ChangeQuantity(itemId, request.Quantity.Value, check.Rules, now);
                }

                if (request.PreparationCode is not null)
                {
                    cart.ChangePreparation(itemId, check.Preparation?.PreparationMethodId, check.Rules, now);
                }

                await carts.SaveChangesAsync(ct);
                return cart;
            },
            cancellationToken);
        return await PriceAsync(cart, cancellationToken);
    }

    public async Task<CartView> RemoveItemAsync(CartOwner owner, Guid itemId, CancellationToken cancellationToken)
    {
        var cart = await runner.RunAsync(
            async ct =>
            {
                var cart = await RequireActiveAsync(owner, ct);
                cart.RemoveItem(itemId, clock.UtcNow);
                await carts.SaveChangesAsync(ct);
                return cart;
            },
            cancellationToken);
        return await PriceAsync(cart, cancellationToken);
    }

    public async Task<CartView> ClearAsync(CartOwner owner, CancellationToken cancellationToken)
    {
        var cart = await runner.RunAsync(
            async ct =>
            {
                var cart = await RequireActiveAsync(owner, ct);
                cart.Clear(clock.UtcNow);
                await carts.SaveChangesAsync(ct);
                return cart;
            },
            cancellationToken);
        return await PriceAsync(cart, cancellationToken);
    }

    /// <summary>
    /// <c>PUT /cart/location</c> (CART-R25). Returns the dropped lines instead of a cart when the change would drop
    /// lines and <paramref name="force"/> is false (409 CART_LOCATION_CHANGED, nothing saved).
    /// </summary>
    public async Task<(CartView? Cart, IReadOnlyList<DroppedLine> Conflicts)> ChangeLocationAsync(
        CartOwner owner,
        string pincode,
        bool force,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var issuedToken = owner.IsCustomer || owner.GuestToken is not null ? null : GuestTokens.Create();
        var removed = new List<string>();
        var outcome = await runner.RunAsync(
            async ct =>
            {
                removed.Clear();
                var cart = await LoadOrCreateAsync(owner, issuedToken, ct);
                var target = await ResolveAsync(pincode, ct);
                if (cart.FulfillmentLocationId != target.FulfillmentLocationId && cart.Items.Count > 0)
                {
                    var priced = await pricer.PriceAsync(new PricingRequest(cart.Id, target, LinesOf(cart), null, null, clock.UtcNow), ct);
                    var dropped = priced.View.Lines
                        .Where(l => l.Availability.Status == LineAvailability.NotSoldHere.ToCode() || l.Issues.Any(i => i.Code is CartErrors.ProductNotAvailable or CartErrors.PriceNotAvailable or CartErrors.PreparationNotAvailable))
                        .Select(l => (Line: l, Code: l.Issues.Count > 0 ? l.Issues[0].Code : CartErrors.PriceNotAvailable))
                        .ToList();
                    if (dropped.Count > 0 && !force)
                    {
                        return (Cart: (ShoppingCart?)null, Conflicts: (IReadOnlyList<DroppedLine>)[.. dropped.Select(d => new DroppedLine(d.Line.Id, d.Code))]);
                    }

                    if (dropped.Count > 0)
                    {
                        cart.RemoveItems([.. dropped.Select(d => d.Line.Id)], clock.UtcNow);
                        removed.AddRange(dropped.Select(d => $"{d.Line.ProductName} was removed: not sold at your new location."));
                    }
                }

                cart.SetLocation(target.FulfillmentLocationId, target.Pincode, clock.UtcNow);
                await carts.SaveChangesAsync(ct);
                return (Cart: (ShoppingCart?)cart, Conflicts: (IReadOnlyList<DroppedLine>)[]);
            },
            cancellationToken);
        if (outcome.Cart is null)
        {
            return (null, outcome.Conflicts);
        }

        var view = await PriceAsync(outcome.Cart, cancellationToken);
        view = view with { Messages = [.. removed, .. view.Messages] };
        return (issuedToken is null ? view : view with { CartToken = issuedToken }, []);
    }

    /// <summary><c>PUT /cart/coupon</c>: customer carts only; the coupon must apply now (CART-R23).</summary>
    public async Task<CartView> ApplyCouponAsync(Guid customerId, string code, CancellationToken cancellationToken)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length == 0)
        {
            throw new RefusalException(new FieldError("code", "VALIDATION_FAILED", "Enter a coupon code."));
        }

        var owner = new CartOwner(customerId, null);
        var cart = await runner.RunAsync(
            async ct =>
            {
                var cart = await RequireActiveAsync(owner, ct);
                var location = await EnsureLocationAsync(cart, null, ct);
                var priced = await pricer.PriceAsync(new PricingRequest(cart.Id, location, LinesOf(cart), null, customerId, clock.UtcNow), ct);
                var cityId = location.CityId ?? throw new DomainException(CartErrors.PincodeNotServiceable, "We do not deliver to your pincode; change the delivery pincode.", "pincode");
                var hasQualifyingOrder = await ordering.HasQualifyingOrderAsync(customerId, ct);
                var lines = priced.Lines.Where(l => l.IsSellable).Select(l => new CouponLine(l.LineId, l.ProductId, l.CategoryId, l.EstimatedTotalPaise)).ToList();
                var evaluation = await promotions.EvaluateAsync(new EvaluateCouponRequest(normalized, customerId, cityId, hasQualifyingOrder, clock.UtcNow, lines), ct);
                if (evaluation.Status != CouponApplicationStatus.Applied)
                {
                    throw new DomainException(evaluation.RefusalCode ?? "COUPON_INVALID", CartPricer.CouponMessage(evaluation.RefusalCode), "code");
                }

                cart.ApplyCoupon(evaluation.Code, clock.UtcNow);
                await carts.SaveChangesAsync(ct);
                return cart;
            },
            cancellationToken);
        return await PriceAsync(cart, cancellationToken);
    }

    public async Task<CartView> RemoveCouponAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var cart = await runner.RunAsync(
            async ct =>
            {
                var cart = await RequireActiveAsync(new CartOwner(customerId, null), ct);
                cart.RemoveCoupon(clock.UtcNow);
                await carts.SaveChangesAsync(ct);
                return cart;
            },
            cancellationToken);
        return await PriceAsync(cart, cancellationToken);
    }

    /// <summary>Guest cart merge after login (CART-R27..R30), its own transaction; idempotent per token.</summary>
    public Task<GuestCartMergeResult> MergeGuestCartAsync(string guestToken, Guid customerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(guestToken))
        {
            return Task.FromResult(new GuestCartMergeResult(null, 0, 0));
        }

        var hash = GuestTokens.Hash(guestToken);
        return runner.RunAsync(
            async ct =>
            {
                var guest = await carts.FindByTokenHashAsync(hash, ct);
                if (guest is null || guest.Status != CartStatus.Active)
                {
                    return new GuestCartMergeResult(null, 0, 0);
                }

                var now = clock.UtcNow;
                var target = await carts.FindActiveByCustomerAsync(customerId, ct);
                if (target is null)
                {
                    target = ShoppingCart.ForCustomer(customerId, now);
                    carts.Add(target);
                }

                var variants = await pricer.LoadVariantsAsync([.. guest.Items.Select(i => i.VariantId)], ct);
                var result = target.MergeFrom(guest, id => variants.TryGetValue(id, out var v) ? v.MaxOrder : null, now);
                await carts.SaveChangesAsync(ct);
                return new GuestCartMergeResult(target.Id, result.MergedLineCount, result.DroppedLineCount);
            },
            cancellationToken);
    }

    /// <summary>Reorder: adds lines that pass CART-R16; others are skipped and named in messages (ORD-R59).</summary>
    public async Task<CartView> AddLinesAsync(Guid customerId, string pincode, IReadOnlyList<CartLineToAdd> lines, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var skipped = new List<string>();
        var owner = new CartOwner(customerId, null);
        var cart = await runner.RunAsync(
            async ct =>
            {
                skipped.Clear();
                var cart = await LoadOrCreateAsync(owner, null, ct);
                var location = await EnsureLocationAsync(cart, pincode, ct);
                var variants = await pricer.LoadVariantsAsync([.. lines.Select(l => l.VariantId)], ct);
                var resolved = lines.Select(l => CartPricer.Resolve(new PricingLine(Guid.Empty, l.VariantId, l.PreparationMethodId, l.Quantity), variants.GetValueOrDefault(l.VariantId))).ToList();
                var prices = await pricer.LoadPricesAsync(location.FulfillmentLocationId, resolved, clock.UtcNow, ct);
                var stock = await pricer.LoadAvailabilityAsync(location.FulfillmentLocationId, [.. variants.Keys], ct);
                foreach (var line in resolved)
                {
                    var name = line.Variant?.ProductName ?? "An item";
                    try
                    {
                        var check = CheckResolved(line, prices, stock, line.Line.Quantity, cart, null);
                        cart.AddItem(line.Line.VariantId, line.Preparation?.PreparationMethodId, line.Line.Quantity, check.Rules, clock.UtcNow);
                    }
                    catch (DomainException refusal)
                    {
                        skipped.Add($"{name} was not added: {refusal.Message}");
                    }
                }

                await carts.SaveChangesAsync(ct);
                return cart;
            },
            cancellationToken);
        var view = await PriceAsync(cart, cancellationToken);
        return view with { Messages = [.. skipped, .. view.Messages] };
    }

    /// <summary>ICartApi.Quote: the customer's cart priced for a delivery pincode (CART-R2, ORD-R5, ORD-R6).</summary>
    public async Task<CartQuote?> QuoteAsync(CartQuoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cart = await carts.FindActiveByCustomerAsync(request.CustomerId, cancellationToken);
        if (cart is null)
        {
            return null;
        }

        var location = await ResolveAsync(request.Pincode, cancellationToken);
        var priced = await pricer.PriceAsync(new PricingRequest(cart.Id, location, LinesOf(cart), cart.CouponCode, request.CustomerId, request.At), cancellationToken);
        return new CartQuote(
            cart.Id,
            location.Pincode,
            location.FulfillmentLocationId,
            location.FulfillmentLocationCode,
            location.CityId!.Value,
            location.ServiceAreaId!.Value,
            location.TimeZone,
            cart.FulfillmentLocationId == location.FulfillmentLocationId,
            priced.MaxOverweightBp,
            priced.DeliveryFeeTaxRateBp,
            cart.CouponCode,
            priced.View,
            priced.Lines);
    }

    public async Task<CheckoutCart?> GetCheckoutCartAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var cart = await carts.FindActiveByCustomerAsync(customerId, cancellationToken);
        return cart is null
            ? null
            : new CheckoutCart(cart.Id, cart.FulfillmentLocationId, cart.Pincode, cart.CouponCode, [.. cart.Items.Select(i => new CheckoutCartLine(i.Id, i.VariantId, i.PreparationMethodId, i.Quantity))]);
    }

    /// <summary>Marks the cart CONVERTED on the shared unit of work (checkout transaction); the caller saves.</summary>
    public async Task MarkConvertedAsync(Guid cartId, CancellationToken cancellationToken)
    {
        var cart = await carts.FindByIdAsync(cartId, cancellationToken)
            ?? throw new NotFoundException(CartErrors.CartNotFound, "The cart does not exist.");
        if (cart.Status != CartStatus.Active)
        {
            throw new ConflictException("CONCURRENT_SAVE", "The cart was already used for another order; reload the cart.");
        }

        cart.MarkConverted(clock.UtcNow);
    }

    private static CartView EmptyView() => new(null, null, null, null, [], null, CartTotalsView.Empty, false, [new CartBlockerView(CartErrors.CartEmpty, null, "Add an item to your cart to check out.")], []);

    private static List<PricingLine> LinesOf(ShoppingCart cart) => [.. cart.Items.Select(i => new PricingLine(i.Id, i.VariantId, i.PreparationMethodId, i.Quantity))];

    private async Task<CartView> PriceAsync(ShoppingCart cart, CancellationToken cancellationToken)
    {
        var location = cart.HasLocation ? await LocationOfAsync(cart, cancellationToken) : null;
        var couponCode = cart.CustomerId is null ? null : cart.CouponCode;
        var priced = await pricer.PriceAsync(new PricingRequest(cart.Id, location, LinesOf(cart), couponCode, cart.CustomerId, clock.UtcNow), cancellationToken);
        return priced.View;
    }

    private async Task<ShoppingCart?> FindActiveAsync(CartOwner owner, CancellationToken cancellationToken)
    {
        if (owner.CustomerId is { } customerId)
        {
            return await carts.FindActiveByCustomerAsync(customerId, cancellationToken);
        }

        if (owner.GuestToken is not { } token)
        {
            return null;
        }

        var cart = await carts.FindByTokenHashAsync(GuestTokens.Hash(token), cancellationToken);
        return cart is { Status: CartStatus.Active } ? cart : null;
    }

    private async Task<ShoppingCart> RequireActiveAsync(CartOwner owner, CancellationToken cancellationToken)
        => await FindActiveAsync(owner, cancellationToken)
            ?? throw new NotFoundException(CartErrors.CartNotFound, "There is no active cart; add an item to start a cart.");

    private async Task<ShoppingCart> LoadOrCreateAsync(CartOwner owner, string? issuedToken, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (owner.CustomerId is { } customerId)
        {
            var existing = await carts.FindActiveByCustomerAsync(customerId, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            var created = ShoppingCart.ForCustomer(customerId, now);
            carts.Add(created);
            return created;
        }

        if (owner.GuestToken is not null)
        {
            return await RequireActiveAsync(owner, cancellationToken);
        }

        var guest = ShoppingCart.ForGuest(GuestTokens.Hash(issuedToken!), now);
        carts.Add(guest);
        return guest;
    }

    private async Task<PricingLocation> EnsureLocationAsync(ShoppingCart cart, string? contextPincode, CancellationToken cancellationToken)
    {
        if (cart.HasLocation)
        {
            return await LocationOfAsync(cart, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(contextPincode))
        {
            throw new DomainException(CartErrors.LocationRequired, "Send your delivery pincode (X-Pincode header or pincode query) with the first item.", "pincode");
        }

        var location = await ResolveAsync(contextPincode, cancellationToken);
        cart.SetLocation(location.FulfillmentLocationId, location.Pincode, clock.UtcNow);
        return location;
    }

    /// <summary>Resolves a pincode to a serviceable location (422 PINCODE_NOT_SERVICEABLE otherwise).</summary>
    private async Task<PricingLocation> ResolveAsync(string pincode, CancellationToken cancellationToken)
    {
        var trimmed = (pincode ?? string.Empty).Trim();
        var serviceability = trimmed.Length == 0 ? null : await locations.ResolvePincodeAsync(trimmed, cancellationToken);
        if (serviceability is not { IsServiceable: true, FulfillmentLocationId: { } locationId, CityId: not null, ServiceAreaId: not null })
        {
            throw new DomainException(CartErrors.PincodeNotServiceable, "We do not deliver to this pincode yet; enter another pincode.", "pincode");
        }

        var summary = FirstOrNull(await locations.GetFulfillmentLocationsAsync([locationId], cancellationToken));
        return ToLocation(serviceability, locationId, summary);
    }

    private async Task<PricingLocation> LocationOfAsync(ShoppingCart cart, CancellationToken cancellationToken)
    {
        var locationId = cart.FulfillmentLocationId!.Value;
        var serviceability = await locations.ResolvePincodeAsync(cart.Pincode!, cancellationToken);
        var summary = FirstOrNull(await locations.GetFulfillmentLocationsAsync([locationId], cancellationToken));
        if (serviceability is { IsServiceable: true } && serviceability.FulfillmentLocationId == locationId)
        {
            return ToLocation(serviceability, locationId, summary);
        }

        return new PricingLocation(locationId, summary?.Code, summary?.CityId, null, cart.Pincode!, summary?.TimeZone, 0, null, 0, IsServiceable: false);
    }

    private static FulfillmentLocationSummary? FirstOrNull(IReadOnlyList<FulfillmentLocationSummary> summaries) => summaries.Count > 0 ? summaries[0] : null;

    private static PricingLocation ToLocation(Serviceability serviceability, Guid locationId, FulfillmentLocationSummary? summary) => new(
        locationId,
        summary?.Code,
        serviceability.CityId,
        serviceability.ServiceAreaId,
        serviceability.Pincode,
        serviceability.TimeZone ?? summary?.TimeZone,
        serviceability.DeliveryFeePaise ?? 0,
        serviceability.FreeDeliveryAbovePaise,
        serviceability.MinOrderPaise ?? 0,
        IsServiceable: true);

    private async Task<LineCheck> CheckLineAsync(
        PricingLocation location,
        Guid variantId,
        string? preparationCode,
        QuantityRequest quantity,
        ShoppingCart cart,
        Guid? existingItemId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quantity);
        var variants = await pricer.LoadVariantsAsync([variantId], cancellationToken);
        var variant = variants.GetValueOrDefault(variantId);
        if (variant is not { IsActive: true })
        {
            throw new DomainException(CartErrors.ProductNotAvailable, "This product is not available; choose another product.", "variantId");
        }

        PreparationInfo? preparation;
        if (preparationCode is null)
        {
            preparation = CartPricer.DefaultPreparation(variant);
        }
        else
        {
            preparation = variant.Preparations.FirstOrDefault(p => p.IsActive && string.Equals(p.Code, preparationCode.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new DomainException(CartErrors.PreparationNotAvailable, "This preparation is not offered for the product; choose another preparation.", "preparationCode");
        }

        if (!string.Equals(quantity.Unit, LineCalculator.QuantityUnitCode(variant.SellUnit), StringComparison.Ordinal))
        {
            throw new RefusalException(new FieldError("quantity.unit", CartErrors.QuantityUnitInvalid, $"Quantity unit must be {LineCalculator.QuantityUnitCode(variant.SellUnit)} for this product."));
        }

        var resolved = new ResolvedLine(new PricingLine(Guid.Empty, variantId, preparation?.PreparationMethodId, quantity.Value), variant, preparation, null);
        var prices = await pricer.LoadPricesAsync(location.FulfillmentLocationId, [resolved], clock.UtcNow, cancellationToken);
        var stock = await pricer.LoadAvailabilityAsync(location.FulfillmentLocationId, [variantId], cancellationToken);
        return CheckResolved(resolved, prices, stock, quantity.Value, cart, existingItemId);
    }

    /// <summary>CART-R16 checks after lookup: active, quantity rules, price, stock for the line's new total.</summary>
    private static LineCheck CheckResolved(
        ResolvedLine resolved,
        Dictionary<(Guid VariantId, Guid? PreparationMethodId), LinePrice> prices,
        Dictionary<Guid, VariantAvailability> stock,
        int quantity,
        ShoppingCart cart,
        Guid? existingItemId)
    {
        var variant = resolved.Variant;
        if (variant is not { IsActive: true })
        {
            throw new DomainException(CartErrors.ProductNotAvailable, "This product is not available; choose another product.", "variantId");
        }

        if (resolved.Issue is not null)
        {
            throw new DomainException(resolved.Issue, "This preparation is not offered for the product; choose another preparation.", "preparationCode");
        }

        var rules = new QuantityRules(variant.MinOrder, variant.MaxOrder, variant.OrderStep);
        if (rules.Check(quantity) is { } error)
        {
            throw new RefusalException(error);
        }

        var preparationId = resolved.Preparation?.PreparationMethodId;
        var price = prices.GetValueOrDefault((variant.VariantId, preparationId))
            ?? throw new DomainException(CartErrors.PriceNotAvailable, "This product is not sold at your location; choose another product.", "variantId");
        var facts = LineCalculator.FactsOf(variant, price);
        if (!SharedKernel.Pricing.LinePricing.IsSupported(facts.Unit, facts.ChargeBasis))
        {
            throw new DomainException(CartErrors.PreparationChargeNotAvailable, "This preparation cannot be priced right now; choose another preparation.", "preparationCode");
        }

        var available = stock.GetValueOrDefault(variant.VariantId)
            ?? throw new DomainException(CartErrors.StockItemMissing, "This product is not stocked at your location; choose another product.", "variantId");

        // The stock check covers the whole line after the change (an add sums into an existing line).
        var target = existingItemId is null ? cart.FindLine(variant.VariantId, preparationId) : cart.FindItem(existingItemId.Value);
        var lineQuantity = existingItemId is null ? quantity + (target?.Quantity ?? 0) : quantity;
        var yieldBp = LineCalculator.YieldOf(variant, resolved.Preparation);
        var gross = LineCalculator.GrossQuantity(variant.StockUnit, lineQuantity, yieldBp);
        if (available.AvailableQuantity < gross)
        {
            var net = LineCalculator.AvailableNet(variant, Math.Max(0, available.AvailableQuantity), yieldBp);
            var message = net >= variant.MinOrder && net > 0
                ? $"Only {LineCalculator.FormatQuantity(variant, net)} available; reduce the quantity."
                : "This product is out of stock today.";
            throw new RefusalException(new FieldError(QuantityRules.QuantityField, CartErrors.OutOfStock, message));
        }

        return new LineCheck(variant, resolved.Preparation, rules);
    }

    private sealed record LineCheck(VariantInfo Variant, PreparationInfo? Preparation, QuantityRules Rules);
}
