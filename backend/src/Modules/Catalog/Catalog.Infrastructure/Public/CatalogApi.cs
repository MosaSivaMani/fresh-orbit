using System.Linq.Expressions;
using FreshOrbit.Catalog.Domain.Preparations;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.Catalog.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using DomainProductStatus = FreshOrbit.Catalog.Domain.Products.ProductStatus;
using DomainSellUnit = FreshOrbit.Catalog.Domain.Products.SellUnit;
using DomainStockUnit = FreshOrbit.Catalog.Domain.Products.StockUnit;
using PublicProductStatus = FreshOrbit.Catalog.Public.ProductStatus;
using PublicSellUnit = FreshOrbit.Catalog.Public.SellUnit;
using PublicStockUnit = FreshOrbit.Catalog.Public.StockUnit;

namespace FreshOrbit.Catalog.Infrastructure.Public;

/// <summary><see cref="ICatalogApi"/>: a fixed number of queries per call, never one per variant.</summary>
public sealed class CatalogApi(FreshOrbitDbContext db) : ICatalogApi
{
    public Task<IReadOnlyList<VariantInfo>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(variantIds);
        var ids = variantIds.Distinct().ToList();
        return LoadVariantsAsync(v => ids.Contains(v.Id), cancellationToken);
    }

    public Task<IReadOnlyList<VariantInfo>> GetVariantsBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skus);
        var normalized = skus.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim().ToUpperInvariant()).Distinct().ToList();
        return LoadVariantsAsync(v => normalized.Contains(v.Sku), cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetSellableVariantIdsAsync(CancellationToken cancellationToken = default)
        => await (from v in db.Set<ProductVariant>()
                  join p in db.Set<Product>() on v.ProductId equals p.Id
                  where v.IsActive && p.Status == DomainProductStatus.Active && v.Preparations.Any(x => x.IsActive)
                  select v.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductSummary>> GetProductSummariesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        var ids = productIds.Distinct().ToList();
        var rows = await db.Set<Product>().AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Slug, p.Status, ImageKey = p.Images.OrderBy(i => i.SortOrder).Select(i => i.StorageKey).FirstOrDefault() })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new ProductSummary(r.Id, r.Name, r.Slug, r.ImageKey, Map(r.Status)))];
    }

    private async Task<IReadOnlyList<VariantInfo>> LoadVariantsAsync(Expression<Func<ProductVariant, bool>> filter, CancellationToken cancellationToken)
    {
        var variants = await db.Set<ProductVariant>().AsNoTracking().Include(v => v.Preparations).Where(filter).ToListAsync(cancellationToken);
        if (variants.Count == 0)
        {
            return [];
        }

        var productIds = variants.Select(v => v.ProductId).Distinct().ToList();
        var products = await db.Set<Product>().AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new ProductRow(p.Id, p.CategoryId, p.Name, p.Slug, p.HsnCode, p.TaxCategoryCode, p.Status, p.Images.OrderBy(i => i.SortOrder).Select(i => i.StorageKey).FirstOrDefault()))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var methods = await db.Set<PreparationMethod>().AsNoTracking().ToDictionaryAsync(m => m.Id, cancellationToken);
        return [.. variants.Select(v =>
        {
            var product = products[v.ProductId];
            return new VariantInfo(
                v.Id,
                v.ProductId,
                product.CategoryId,
                product.Name,
                product.Slug,
                v.Name,
                v.Sku,
                product.ImageKey,
                product.HsnCode ?? string.Empty,
                product.TaxCategoryCode ?? string.Empty,
                Map(v.SellUnit),
                Map(v.StockUnit),
                v.PackSizeGrams,
                v.AvgPieceGrams,
                v.MinOrder,
                v.MaxOrder,
                v.OrderStep,
                v.IsWeightVariable,
                v.IsActive,
                [.. v.Preparations
                    .Select(p => (Preparation: p, Method: methods[p.PreparationMethodId]))
                    .OrderBy(x => x.Method.SortOrder)
                    .Select(x => new PreparationInfo(x.Method.Id, x.Method.Code, x.Method.Name, x.Preparation.YieldBp, x.Preparation.IsDefault, x.Preparation.IsActive))])
            {
                ProductStatus = Map(product.Status),
            };
        })];
    }

    private static PublicSellUnit Map(DomainSellUnit unit) => unit switch
    {
        DomainSellUnit.Kg => PublicSellUnit.Kg,
        DomainSellUnit.Piece => PublicSellUnit.Piece,
        _ => PublicSellUnit.Pack,
    };

    private static PublicStockUnit Map(DomainStockUnit unit) => unit == DomainStockUnit.Gram ? PublicStockUnit.Gram : PublicStockUnit.Piece;

    private static PublicProductStatus Map(DomainProductStatus status) => status switch
    {
        DomainProductStatus.Draft => PublicProductStatus.Draft,
        DomainProductStatus.Active => PublicProductStatus.Active,
        _ => PublicProductStatus.Archived,
    };

    private sealed record ProductRow(Guid Id, Guid CategoryId, string Name, string Slug, string? HsnCode, string? TaxCategoryCode, DomainProductStatus Status, string? ImageKey);
}
