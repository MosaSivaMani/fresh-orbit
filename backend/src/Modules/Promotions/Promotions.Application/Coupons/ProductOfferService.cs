using System.Globalization;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Promotions.Application.Abstractions;
using FreshOrbit.Promotions.Domain.Coupons;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Promotions.Application.Coupons;

/// <summary>
/// PROMO-R26 (Catalog CAT-R26): the coupon line shown on a product. Candidates are the listed coupons usable in the
/// city now (PROMO-R24, not customer-specific) whose scope covers the product (no scope = everything; a category scope
/// covers its sub-categories). The most specific one wins: PRODUCT scope, then CATEGORY, then store-wide; then the
/// lowest minimum order; then the code. The text is composed here; no amount is computed for the customer.
/// </summary>
public sealed class ProductOfferService(IPromotionsReads reads, IPromotionsCatalog catalog) : IProductOfferSource
{
    public async Task<IReadOnlyList<ProductOfferLine>> GetOfferLinesAsync(Guid cityId, IReadOnlyCollection<ProductOfferQuery> products, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(products);
        if (products.Count == 0)
        {
            return [];
        }

        var coupons = await reads.ListVisibleCouponsAsync(cityId, null, at, cancellationToken);
        if (coupons.Count == 0)
        {
            return [];
        }

        var lineages = coupons.Any(c => c.Scopes.Any(s => s.ScopeType == CouponScopeType.Category))
            ? await catalog.GetCategoryLineagesAsync([.. products.Select(p => p.CategoryId).Distinct()], cancellationToken)
            : new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        var lines = new List<ProductOfferLine>();
        foreach (var product in products.DistinctBy(p => p.ProductId))
        {
            var lineage = lineages.GetValueOrDefault(product.CategoryId) ?? [product.CategoryId];
            var best = Pick(coupons, product.ProductId, lineage);
            if (best is not null)
            {
                lines.Add(new ProductOfferLine(product.ProductId, best.Code, Text(best)));
            }
        }

        return lines;
    }

    /// <summary>The coupon to show for a product, or null when none applies.</summary>
    public static Coupon? Pick(IEnumerable<Coupon> coupons, Guid productId, IReadOnlyCollection<Guid> categoryLineage)
    {
        ArgumentNullException.ThrowIfNull(coupons);
        ArgumentNullException.ThrowIfNull(categoryLineage);
        return coupons
            .Select(c => (Coupon: c, Rank: Specificity(c, productId, categoryLineage)))
            .Where(x => x.Rank is not null)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Coupon.MinOrderPaise)
            .ThenBy(x => x.Coupon.Code, StringComparer.Ordinal)
            .Select(x => x.Coupon)
            .FirstOrDefault();
    }

    /// <summary>
    /// One sentence: "15% off up to ₹100", "₹50 off" or "Free delivery", then " on orders above ₹499" when there is a
    /// minimum, " on your first order" when first-order only, then " with code CODE".
    /// </summary>
    public static string Text(Coupon coupon)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        var text = coupon.DiscountType switch
        {
            DiscountType.Percent => coupon.MaxDiscountPaise is { } cap
                ? $"{Percent(coupon.DiscountValue)} off up to {Rupees(cap)}"
                : $"{Percent(coupon.DiscountValue)} off",
            DiscountType.Flat => $"{Rupees(coupon.DiscountValue)} off",
            _ => "Free delivery",
        };
        if (coupon.MinOrderPaise > 0)
        {
            text += $" on orders above {Rupees(coupon.MinOrderPaise)}";
        }

        if (coupon.FirstOrderOnly)
        {
            text += " on your first order";
        }

        return $"{text} with code {coupon.Code}";
    }

    /// <summary>0 = product scope, 1 = category scope, 2 = store-wide; null = does not apply.</summary>
    private static int? Specificity(Coupon coupon, Guid productId, IReadOnlyCollection<Guid> lineage)
    {
        if (coupon.Scopes.Count == 0)
        {
            return 2;
        }

        if (coupon.Scopes.Any(s => s.ScopeType == CouponScopeType.Product && s.ScopeId == productId))
        {
            return 0;
        }

        return coupon.Scopes.Any(s => s.ScopeType == CouponScopeType.Category && lineage.Contains(s.ScopeId)) ? 1 : null;
    }

    private static string Percent(long bp)
        => (bp / (decimal)(BasisPoints.Whole / 100)).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string Rupees(long paise)
        => "₹" + (paise / (decimal)Money.PaisePerRupee).ToString(paise % Money.PaisePerRupee == 0 ? "0" : "0.00", CultureInfo.InvariantCulture);
}
