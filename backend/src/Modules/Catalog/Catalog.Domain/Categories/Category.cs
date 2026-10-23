using FreshOrbit.Catalog.Domain.Categories.Events;
using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Catalog.Domain.Categories;

/// <summary>A browse category; at most two levels (parent → child, CAT-R2).</summary>
public sealed class Category : AggregateRoot
{
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 500;

    private Category()
    {
        Slug = Name = string.Empty;
    }

    private Category(Guid? parentId, string slug, string name, string? description, int sortOrder)
    {
        ParentId = parentId;
        Slug = slug;
        Name = name;
        Description = description;
        SortOrder = sortOrder;
        IsActive = true;
    }

    public Guid? ParentId { get; private set; }

    public string Slug { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public string? ImageKey { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Deactivating hides the category, its children and their products (CAT-R10); nothing is refused.</summary>
    public bool IsActive { get; private set; }

    /// <param name="parent">The parent category, which must itself be top-level (CAT-R2), or null.</param>
    public static Category Create(Category? parent, string? slug, string? name, string? description, int sortOrder)
    {
        var errors = new List<FieldError>();
        ValidateParent(errors, null, parent, hasChildren: false);
        var resolvedSlug = string.IsNullOrWhiteSpace(slug) ? Slugs.FromName(name) : slug;
        ValidateFields(errors, resolvedSlug, name, description);
        ThrowIfAny(errors);
        var category = new Category(parent?.Id, resolvedSlug, name!.Trim(), Normalize(description), sortOrder);
        category.Raise(new CategoryChanged(category.Id, category.IsActive));
        return category;
    }

    /// <param name="parent">New parent (top-level, not this category) or null for top-level.</param>
    /// <param name="hasChildren">Whether this category has children (then it must stay top-level).</param>
    public void Update(Category? parent, bool hasChildren, string? slug, string? name, string? description, string? imageKey, int sortOrder, bool isActive)
    {
        var errors = new List<FieldError>();
        ValidateParent(errors, this, parent, hasChildren);
        var resolvedSlug = string.IsNullOrWhiteSpace(slug) ? Slug : slug;
        ValidateFields(errors, resolvedSlug, name, description);
        ThrowIfAny(errors);
        ParentId = parent?.Id;
        Slug = resolvedSlug;
        Name = name!.Trim();
        Description = Normalize(description);
        ImageKey = Normalize(imageKey);
        SortOrder = sortOrder;
        IsActive = isActive;
        Raise(new CategoryChanged(Id, IsActive));
    }

    private static void ValidateParent(List<FieldError> errors, Category? self, Category? parent, bool hasChildren)
    {
        if (parent is null)
        {
            return;
        }

        if (self is not null && parent.Id == self.Id)
        {
            errors.Add(new FieldError("parentId", CatalogErrors.ValidationFailed, "A category cannot be its own parent."));
        }
        else if (parent.ParentId is not null || hasChildren)
        {
            errors.Add(new FieldError("parentId", CatalogErrors.ValidationFailed, "Categories have at most two levels; choose a top-level parent."));
        }
    }

    private static void ValidateFields(List<FieldError> errors, string? slug, string? name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
        {
            errors.Add(new FieldError("name", CatalogErrors.ValidationFailed, $"Enter a name of 1 to {NameMaxLength} characters."));
        }

        if (!Slugs.IsValid(slug))
        {
            errors.Add(new FieldError("slug", CatalogErrors.ValidationFailed, "Enter a slug of lowercase letters, digits and single hyphens, at most 80 characters."));
        }

        if (description is { Length: > DescriptionMaxLength })
        {
            errors.Add(new FieldError("description", CatalogErrors.ValidationFailed, $"Enter a description of at most {DescriptionMaxLength} characters."));
        }
    }

    private static void ThrowIfAny(List<FieldError> errors)
    {
        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public interface ICategoryRepository
{
    Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, Guid? excludingId, CancellationToken cancellationToken);

    Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken);

    void Add(Category category);
}
