namespace FreshOrbit.Catalog.Public;

// Storefront facts owned by modules that use Catalog. Catalog uses no other module (docs/architecture), so the owners
// implement these interfaces (the ICityActivationCheck pattern, D4) and Catalog's browse endpoints ask every registered
// implementation. With none registered the facts are simply absent (null or empty), never invented.

/// <summary>Real sales ranking per fulfillment location (implemented by Ordering, CAT-R24).</summary>
public interface IBestsellerSource
{
    /// <summary>
    /// Up to <paramref name="limit"/> product ids most often ordered at the location since <paramref name="since"/>, best
    /// first; empty when nothing was sold. Orders that did not complete a sale (unpaid, failed, cancelled, returned) and
    /// lines that were not delivered (cancelled, substituted away) do not count.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetBestsellingProductIdsAsync(Guid fulfillmentLocationId, DateTimeOffset since, int limit, CancellationToken cancellationToken = default);
}

/// <summary>Freshness of the stock a variant is sold from at a location (implemented by Inventory, CAT-R25).</summary>
public interface IVariantFreshnessSource
{
    /// <summary>
    /// For each requested variant with sellable stock at <paramref name="at"/>: the batch the next order is packed from
    /// (FEFO). Variants without such a batch are absent.
    /// </summary>
    Task<IReadOnlyList<VariantFreshness>> GetFreshnessAsync(Guid fulfillmentLocationId, IReadOnlyCollection<Guid> variantIds, DateTimeOffset at, CancellationToken cancellationToken = default);
}

/// <param name="CaughtOn">Catch or harvest date as recorded at receipt; null when the supplier did not state it.</param>
/// <param name="ReceivedAt">When the batch was received at the location.</param>
public sealed record VariantFreshness(Guid VariantId, DateOnly? CaughtOn, DateTimeOffset ReceivedAt);

/// <summary>Listed coupons that apply to products (implemented by Promotions, CAT-R26).</summary>
public interface IProductOfferSource
{
    /// <summary>
    /// For each product that a listed, currently valid coupon of the city applies to: the one coupon to show. Products
    /// without one are absent.
    /// </summary>
    Task<IReadOnlyList<ProductOfferLine>> GetOfferLinesAsync(Guid cityId, IReadOnlyCollection<ProductOfferQuery> products, DateTimeOffset at, CancellationToken cancellationToken = default);
}

public sealed record ProductOfferQuery(Guid ProductId, Guid CategoryId);

/// <param name="Text">One display sentence composed by the server (e.g. "15% off up to ₹100 with code FRESH15").</param>
public sealed record ProductOfferLine(Guid ProductId, string CouponCode, string Text);
