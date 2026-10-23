using FreshOrbit.Cart.Domain.Carts.Events;
using FreshOrbit.Cart.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Cart.Domain.Carts;

/// <summary>Stored as ACTIVE / CONVERTED / ABANDONED.</summary>
public enum CartStatus
{
    Active,
    Converted,
    Abandoned,
}

/// <summary>A variant's ordering limits in its stock unit (grams or pieces).</summary>
public sealed record QuantityRules(int MinOrder, int MaxOrder, int OrderStep)
{
    public const string QuantityField = "quantity.value";

    /// <summary>The first quantity rule the value breaks (CART-R16), or null.</summary>
    public FieldError? Check(int quantity, string field = QuantityField)
    {
        if (quantity < MinOrder || quantity <= 0)
        {
            return new FieldError(field, CartErrors.QuantityBelowMin, $"Quantity must be at least {MinOrder}.");
        }

        if (quantity > MaxOrder)
        {
            return new FieldError(field, CartErrors.QuantityAboveMax, $"Quantity must be at most {MaxOrder}.");
        }

        return OrderStep > 0 && quantity % OrderStep != 0
            ? new FieldError(field, CartErrors.QuantityStepInvalid, $"Quantity must be a multiple of {OrderStep}.")
            : null;
    }
}

/// <summary>One cart line: a variant and preparation with a net quantity in the variant's stock unit. Never a price.</summary>
public sealed class CartItem : Entity
{
    private CartItem()
    {
    }

    internal CartItem(Guid cartId, Guid variantId, Guid? preparationMethodId, int quantity, DateTimeOffset addedAt)
    {
        CartId = cartId;
        VariantId = variantId;
        PreparationMethodId = preparationMethodId;
        Quantity = quantity;
        AddedAt = addedAt;
    }

    public Guid CartId { get; private set; }

    public Guid VariantId { get; private set; }

    public Guid? PreparationMethodId { get; private set; }

    /// <summary>Grams (KG variants), pieces (PIECE) or pack count (PACK).</summary>
    public int Quantity { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    internal void SetQuantity(int quantity) => Quantity = quantity;

    internal void SetPreparation(Guid? preparationMethodId) => PreparationMethodId = preparationMethodId;
}

/// <summary>
/// A shopping cart (<c>cart.carts</c>, row version <c>xmin</c>) owned by a customer or by a guest token hash.
/// Stores lines only; prices, availability and coupon validity are computed on every read (CART-R1).
/// </summary>
public sealed class ShoppingCart : AggregateRoot
{
    public const int MaxLines = 30;
    public static readonly TimeSpan CustomerLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan GuestLifetime = TimeSpan.FromDays(7);

    private readonly List<CartItem> _items = [];

    private ShoppingCart()
    {
    }

    private ShoppingCart(Guid? customerId, string? guestTokenHash, DateTimeOffset now)
    {
        CustomerId = customerId;
        GuestTokenHash = guestTokenHash;
        Status = CartStatus.Active;
        Touch(now);
    }

    public Guid? CustomerId { get; private set; }

    /// <summary>SHA-256 (hex) of the guest token; the token itself is never stored (CART-R22).</summary>
    public string? GuestTokenHash { get; private set; }

    public Guid? FulfillmentLocationId { get; private set; }

    public string? Pincode { get; private set; }

    /// <summary>Customer carts only (CART-R23).</summary>
    public string? CouponCode { get; private set; }

    public CartStatus Status { get; private set; }

    public DateTimeOffset LastActivityAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public IReadOnlyList<CartItem> Items => _items;

    public bool HasLocation => FulfillmentLocationId is not null;

    public static ShoppingCart ForCustomer(Guid customerId, DateTimeOffset now) => new(customerId, null, now);

    public static ShoppingCart ForGuest(string guestTokenHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guestTokenHash);
        return new ShoppingCart(null, guestTokenHash, now);
    }

    public CartItem? FindItem(Guid itemId) => _items.Find(i => i.Id == itemId);

    public CartItem? FindLine(Guid variantId, Guid? preparationMethodId)
        => _items.Find(i => i.VariantId == variantId && i.PreparationMethodId == preparationMethodId);

    /// <summary>
    /// Adds a line or adds to the matching variant + preparation line (CART-R16, R17). The caller has checked the
    /// variant, preparation, unit, price and stock.
    /// </summary>
    public CartItem AddItem(Guid variantId, Guid? preparationMethodId, int quantity, QuantityRules rules, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rules);
        EnsureActive();
        ThrowIf(rules.Check(quantity));
        var existing = FindLine(variantId, preparationMethodId);
        if (existing is not null)
        {
            var sum = existing.Quantity + quantity;
            if (sum > rules.MaxOrder)
            {
                throw new RefusalException(new FieldError(QuantityRules.QuantityField, CartErrors.QuantityAboveMax, $"This line would hold {sum}; the most you can order is {rules.MaxOrder}."));
            }

            existing.SetQuantity(sum);
            Touch(now);
            return existing;
        }

        if (_items.Count >= MaxLines)
        {
            throw new DomainException(CartErrors.CartItemLimit, $"A cart can hold at most {MaxLines} lines; remove a line first.", "variantId");
        }

        var item = new CartItem(Id, variantId, preparationMethodId, quantity, now);
        _items.Add(item);
        Touch(now);
        return item;
    }

    public void ChangeQuantity(Guid itemId, int quantity, QuantityRules rules, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rules);
        EnsureActive();
        var item = RequireItem(itemId);
        ThrowIf(rules.Check(quantity));
        item.SetQuantity(quantity);
        Touch(now);
    }

    /// <summary>
    /// Moves a line to another preparation; when a line with that preparation exists the two merge, capped at
    /// <see cref="QuantityRules.MaxOrder"/> (CART-R18). Returns the line that holds the quantity afterwards.
    /// </summary>
    public CartItem ChangePreparation(Guid itemId, Guid? preparationMethodId, QuantityRules rules, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rules);
        EnsureActive();
        var item = RequireItem(itemId);
        if (item.PreparationMethodId == preparationMethodId)
        {
            return item;
        }

        var target = FindLine(item.VariantId, preparationMethodId);
        if (target is null)
        {
            item.SetPreparation(preparationMethodId);
            Touch(now);
            return item;
        }

        target.SetQuantity(Math.Min(target.Quantity + item.Quantity, rules.MaxOrder));
        _items.Remove(item);
        Touch(now);
        return target;
    }

    public void RemoveItem(Guid itemId, DateTimeOffset now)
    {
        EnsureActive();
        _items.Remove(RequireItem(itemId));
        Touch(now);
    }

    /// <summary>Removes the given lines (location change with force).</summary>
    public void RemoveItems(IReadOnlyCollection<Guid> itemIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        EnsureActive();
        _items.RemoveAll(i => itemIds.Contains(i.Id));
        Touch(now);
    }

    /// <summary>Removes every line and the coupon; the cart stays ACTIVE.</summary>
    public void Clear(DateTimeOffset now)
    {
        EnsureActive();
        _items.Clear();
        CouponCode = null;
        Touch(now);
    }

    public void SetLocation(Guid fulfillmentLocationId, string pincode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pincode);
        EnsureActive();
        FulfillmentLocationId = fulfillmentLocationId;
        Pincode = pincode;
        Touch(now);
    }

    public void ApplyCoupon(string code, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        EnsureActive();
        if (CustomerId is null)
        {
            throw new InvalidOperationException("Only customer carts carry a coupon.");
        }

        CouponCode = code.Trim().ToUpperInvariant();
        Touch(now);
    }

    public void RemoveCoupon(DateTimeOffset now)
    {
        EnsureActive();
        CouponCode = null;
        Touch(now);
    }

    /// <summary>The order was placed from this cart (checkout transaction) or the guest cart was merged.</summary>
    public void MarkConverted(DateTimeOffset now)
    {
        EnsureActive();
        Status = CartStatus.Converted;
        LastActivityAt = now;
    }

    /// <summary>Abandons an ACTIVE cart past its expiry (job). False when not due.</summary>
    public bool AbandonIfDue(DateTimeOffset now)
    {
        if (Status != CartStatus.Active || ExpiresAt > now)
        {
            return false;
        }

        Status = CartStatus.Abandoned;
        Raise(new CartAbandoned(Id, CustomerId, _items.Count, Pincode, FulfillmentLocationId, LastActivityAt, now));
        return true;
    }

    /// <summary>
    /// Merges a guest cart's lines into this customer cart (CART-R28, R29): matching lines add up capped at the
    /// variant's max; other lines are appended while there is room; the guest pincode wins when it has one.
    /// </summary>
    /// <param name="maxOrderOf">Max order of a variant; null when the variant no longer exists (line dropped).</param>
    public CartMergeResult MergeFrom(ShoppingCart guest, Func<Guid, int?> maxOrderOf, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(guest);
        ArgumentNullException.ThrowIfNull(maxOrderOf);
        EnsureActive();
        var merged = 0;
        var dropped = 0;
        foreach (var line in guest.Items)
        {
            if (maxOrderOf(line.VariantId) is not { } max)
            {
                dropped++;
                continue;
            }

            var existing = FindLine(line.VariantId, line.PreparationMethodId);
            if (existing is not null)
            {
                existing.SetQuantity(Math.Min(existing.Quantity + line.Quantity, max));
                merged++;
            }
            else if (_items.Count < MaxLines)
            {
                _items.Add(new CartItem(Id, line.VariantId, line.PreparationMethodId, Math.Min(line.Quantity, max), now));
                merged++;
            }
            else
            {
                dropped++;
            }
        }

        if (guest.FulfillmentLocationId is { } locationId && guest.Pincode is { } pincode)
        {
            FulfillmentLocationId = locationId;
            Pincode = pincode;
        }

        guest.MarkConverted(now);
        Touch(now);
        return new CartMergeResult(merged, dropped);
    }

    private void Touch(DateTimeOffset now)
    {
        LastActivityAt = now;
        ExpiresAt = now + (CustomerId is null ? GuestLifetime : CustomerLifetime);
    }

    private CartItem RequireItem(Guid itemId)
        => FindItem(itemId) ?? throw new NotFoundException(CartErrors.CartItemNotFound, "The cart has no line with this id.");

    private void EnsureActive()
    {
        if (Status != CartStatus.Active)
        {
            throw new NotFoundException(CartErrors.CartNotFound, "This cart is no longer active.");
        }
    }

    private static void ThrowIf(FieldError? error)
    {
        if (error is not null)
        {
            throw new RefusalException(error);
        }
    }
}

public sealed record CartMergeResult(int MergedLineCount, int DroppedLineCount);
