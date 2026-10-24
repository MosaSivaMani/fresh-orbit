using FreshOrbit.Catalog.Application.Admin;
using FreshOrbit.Catalog.Domain.Categories;
using FreshOrbit.Catalog.Domain.Preparations;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Storage;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Catalog.Infrastructure.Reads;

public sealed class CatalogAdminReads(FreshOrbitDbContext db, IFileStorage storage) : ICatalogAdminReads
{
    public async Task<IReadOnlyList<AdminCategoryResponse>> GetCategoryTreeAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var all = await db.Set<Category>().AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(cancellationToken);
        var visible = includeInactive ? all : [.. all.Where(c => c.IsActive)];
        return [.. visible.Where(c => c.ParentId is null).Select(c => ToResponse(c, [.. visible.Where(x => x.ParentId == c.Id).Select(x => ToResponse(x, []))]))];
    }

    public async Task<AdminCategoryResponse?> GetCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var rows = await db.Set<Category>().AsNoTracking().Where(c => c.Id == id || c.ParentId == id).ToListAsync(cancellationToken);
        var category = rows.FirstOrDefault(c => c.Id == id);
        return category is null
            ? null
            : ToResponse(category, [.. rows.Where(c => c.ParentId == id).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(c => ToResponse(c, []))]);
    }

    public async Task<(IReadOnlyList<AdminProductListItem> Items, int TotalCount)> ListProductsAsync(
        string? q,
        Guid? categoryId,
        IReadOnlyCollection<ProductStatus> statuses,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = db.Set<Product>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern) || EF.Functions.ILike(p.Slug, pattern) || p.Variants.Any(v => EF.Functions.ILike(v.Sku, pattern)));
        }

        if (categoryId is { } category)
        {
            query = query.Where(p => p.CategoryId == category);
        }

        if (statuses.Count > 0)
        {
            query = query.Where(p => statuses.Contains(p.Status));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip(skip)
            .Take(take)
            .Select(p => new ProductRow(
                p,
                p.Variants.Count(v => v.IsActive),
                p.Images.OrderBy(i => i.SortOrder).Select(i => i.StorageKey).FirstOrDefault()))
            .ToListAsync(cancellationToken);
        return ([.. rows.Select(r => new AdminProductListItem(
            r.Product.Id,
            r.Product.CategoryId,
            r.Product.Slug,
            r.Product.Name,
            r.Product.Status,
            r.ActiveVariants,
            r.ImageKey is null ? null : storage.GetPublicUrl(ImageRenditions.Key(r.ImageKey, ImageRenditions.Small)),
            r.Product.PublishedAt,
            CatalogFormats.Version(r.Product.Version)))], total);
    }

    public async Task<IReadOnlyList<PreparationMethodResponse>> ListPreparationMethodsAsync(CancellationToken cancellationToken)
        => await db.Set<PreparationMethod>().AsNoTracking()
            .OrderBy(m => m.SortOrder)
            .Select(m => new PreparationMethodResponse(m.Id, m.Code, m.Name, m.Description, m.SortOrder, m.GuidanceYieldBp))
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<VariantLookupItem> Items, int TotalCount)> SearchVariantsAsync(
        string? q,
        IReadOnlyCollection<Guid> variantIds,
        bool activeOnly,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = from v in db.Set<ProductVariant>().AsNoTracking()
                    join p in db.Set<Product>().AsNoTracking() on v.ProductId equals p.Id
                    select new { Variant = v, Product = p };
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{Escape(q.Trim())}%";
            query = query.Where(r => EF.Functions.ILike(r.Product.Name, pattern) || EF.Functions.ILike(r.Variant.Name, pattern) || EF.Functions.ILike(r.Variant.Sku, pattern));
        }

        if (variantIds.Count > 0)
        {
            var ids = variantIds.Distinct().ToList();
            query = query.Where(r => ids.Contains(r.Variant.Id));
        }

        if (activeOnly)
        {
            query = query.Where(r => r.Variant.IsActive && r.Product.Status != ProductStatus.Archived);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(r => r.Product.Name).ThenBy(r => r.Variant.Sku).ThenBy(r => r.Variant.Id)
            .Skip(skip)
            .Take(take)
            .Select(r => new VariantLookupItem(
                r.Variant.Id,
                r.Product.Id,
                r.Product.Name,
                r.Variant.Name,
                r.Variant.Sku,
                r.Variant.SellUnit,
                r.Variant.StockUnit,
                r.Product.Status,
                r.Variant.IsActive))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private static AdminCategoryResponse ToResponse(Category category, IReadOnlyList<AdminCategoryResponse> children) => new(
        category.Id,
        category.ParentId,
        category.Slug,
        category.Name,
        category.Description,
        category.ImageKey,
        category.SortOrder,
        category.IsActive,
        CatalogFormats.Version(category.Version),
        children);

    private sealed record ProductRow(Product Product, int ActiveVariants, string? ImageKey);
}
