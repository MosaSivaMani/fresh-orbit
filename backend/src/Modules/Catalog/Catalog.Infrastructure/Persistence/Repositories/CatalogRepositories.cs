using FreshOrbit.Catalog.Domain.Categories;
using FreshOrbit.Catalog.Domain.Preparations;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Catalog.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository(FreshOrbitDbContext db) : ICategoryRepository
{
    public Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<Category>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, Guid? excludingId, CancellationToken cancellationToken)
        => db.Set<Category>().AnyAsync(c => c.Slug == slug && c.Id != excludingId, cancellationToken);

    public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<Category>().AnyAsync(c => c.ParentId == id, cancellationToken);

    public void Add(Category category) => db.Add(category);
}

public sealed class ProductRepository(FreshOrbitDbContext db) : IProductRepository
{
    public Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken)
        => db.Set<Product>()
            .Include(p => p.Variants).ThenInclude(v => v.Preparations)
            .Include(p => p.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, Guid? excludingId, CancellationToken cancellationToken)
        => db.Set<Product>().AnyAsync(p => p.Slug == slug && p.Id != excludingId, cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken)
        => db.Set<ProductVariant>().AnyAsync(v => v.Sku == sku, cancellationToken);

    public void Add(Product product) => db.Add(product);

    public void MarkChanged(Product product)
    {
        var entry = db.Entry(product);
        if (entry.State == EntityState.Unchanged)
        {
            entry.Property(AuditColumns.ModifiedAt).IsModified = true;
        }
    }
}

public sealed class PreparationMethodRepository(FreshOrbitDbContext db) : IPreparationMethodRepository
{
    public async Task<IReadOnlyList<PreparationMethod>> ListAsync(CancellationToken cancellationToken)
        => await db.Set<PreparationMethod>().AsNoTracking().OrderBy(m => m.SortOrder).ToListAsync(cancellationToken);
}
