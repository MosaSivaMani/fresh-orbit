using FreshOrbit.Catalog.Domain.Categories;
using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Catalog.Application.Admin;

/// <summary>CreateCategory and UpdateCategory (CAT-R1, CAT-R2).</summary>
public sealed class CategoryService(ICategoryRepository categories, ICatalogAdminReads reads, IUnitOfWork unitOfWork)
{
    public async Task<AdminCategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parent = await LoadParentAsync(request.ParentId, cancellationToken);
        var category = Category.Create(parent, request.Slug, request.Name, request.Description, request.SortOrder);
        await EnsureSlugFreeAsync(category.Slug, null, cancellationToken);
        categories.Add(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(category.Id, cancellationToken);
    }

    public async Task<AdminCategoryResponse> UpdateAsync(Guid id, uint expectedVersion, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var category = await categories.GetAsync(id, cancellationToken)
            ?? throw new NotFoundException(CatalogErrors.CategoryNotFound, "No category has this id.");
        unitOfWork.SetExpectedVersion(category, expectedVersion);
        var parent = await LoadParentAsync(request.ParentId, cancellationToken);
        var hasChildren = parent is not null && await categories.HasChildrenAsync(id, cancellationToken);
        category.Update(parent, hasChildren, request.Slug, request.Name, request.Description, request.ImageKey, request.SortOrder, request.IsActive);
        await EnsureSlugFreeAsync(category.Slug, category.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(category.Id, cancellationToken);
    }

    private async Task<Category?> LoadParentAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId is not { } id)
        {
            return null;
        }

        return await categories.GetAsync(id, cancellationToken)
            ?? throw new RefusalException(new FieldError("parentId", CatalogErrors.ValidationFailed, "Choose an existing parent category."));
    }

    private async Task EnsureSlugFreeAsync(string slug, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (await categories.SlugExistsAsync(slug, excludingId, cancellationToken))
        {
            throw new ConflictException(CatalogErrors.SlugTaken, "Another category already uses this slug.", "slug");
        }
    }

    private async Task<AdminCategoryResponse> ReadAsync(Guid id, CancellationToken cancellationToken)
        => await reads.GetCategoryAsync(id, cancellationToken) ?? throw new InvalidOperationException($"Category {id} was just saved but cannot be read.");
}
