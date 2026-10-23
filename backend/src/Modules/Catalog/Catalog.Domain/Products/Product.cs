using System.Text.RegularExpressions;
using FreshOrbit.Catalog.Domain.Products.Events;
using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Catalog.Domain.Products;

/// <summary>The editable fields of a product.</summary>
public sealed record ProductDetails(
    string? Slug,
    string? Name,
    IReadOnlyList<string>? LocalNames,
    string? SpeciesScientificName,
    string? Description,
    SourceType SourceType,
    string? SourceRegion,
    string? StorageInstructions,
    string? HandlingNotes,
    string? HsnCode,
    string? TaxCategoryCode,
    int? FeaturedRank);

/// <summary>A product with its variants, preparations and images (one aggregate, version = <c>products.xmin</c>).</summary>
public sealed partial class Product : AggregateRoot
{
    public const int NameMaxLength = 120;
    public const int MaxLocalNames = 10;
    public const int LocalNameMaxLength = 40;
    public const int TextMaxLength = 2000;
    public const int ShortTextMaxLength = 200;
    public const int MaxImages = 8;

    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductImage> _images = [];

    private Product()
    {
        Slug = Name = string.Empty;
        LocalNames = [];
    }

    private Product(Guid categoryId)
    {
        CategoryId = categoryId;
        Slug = Name = string.Empty;
        LocalNames = [];
        Status = ProductStatus.Draft;
    }

    public Guid CategoryId { get; private set; }

    public string Slug { get; private set; }

    public string Name { get; private set; }

    /// <summary>CAT-R19: up to 10 regional names (Vanjaram, Neymeen…).</summary>
    public string[] LocalNames { get; private set; }

    public string? SpeciesScientificName { get; private set; }

    public string? Description { get; private set; }

    public SourceType SourceType { get; private set; }

    public string? SourceRegion { get; private set; }

    public string? StorageInstructions { get; private set; }

    public string? HandlingNotes { get; private set; }

    public string? HsnCode { get; private set; }

    public string? TaxCategoryCode { get; private set; }

    public ProductStatus Status { get; private set; }

    /// <summary>Position on the home page; null = not featured.</summary>
    public int? FeaturedRank { get; private set; }

    /// <summary>Set on first publish; the slug is locked from then on (CAT-R1).</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    public IReadOnlyList<ProductVariant> Variants => _variants;

    public IReadOnlyList<ProductImage> Images => _images;

    public static Product Create(Guid categoryId, ProductDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var product = new Product(categoryId);
        var errors = new List<FieldError>();
        var slug = string.IsNullOrWhiteSpace(details.Slug) ? Slugs.FromName(details.Name) : details.Slug.Trim();
        product.ApplyDetails(details, slug, errors);
        ThrowIfAny(errors);
        product.Raise(new ProductUpdated(product.Id, ["created"]));
        return product;
    }

    /// <summary>Updates the product fields; a slug change after the first publish is refused (CAT-R1).</summary>
    public void Update(Guid categoryId, ProductDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = new List<FieldError>();
        var slug = string.IsNullOrWhiteSpace(details.Slug) ? Slug : details.Slug.Trim();
        if (PublishedAt is not null && slug != Slug)
        {
            errors.Add(new FieldError("slug", CatalogErrors.ValidationFailed, "The slug cannot change once the product has been published."));
        }

        var before = Snapshot();
        CategoryId = categoryId;
        ApplyDetails(details, slug, errors);
        ThrowIfAny(errors);
        var changed = Changes(before, Snapshot());
        if (changed.Count > 0)
        {
            Raise(new ProductUpdated(Id, changed));
        }
    }

    /// <summary>CAT-R3: DRAFT → ACTIVE, listing every reason it cannot be published.</summary>
    public void Publish(bool categoryActive, Func<Guid, string> methodCode, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(methodCode);
        if (Status != ProductStatus.Draft)
        {
            throw new DomainException(CatalogErrors.InvalidStatusTransition, $"A {Status.ToString().ToUpperInvariant()} product cannot be published; only DRAFT products can.");
        }

        var errors = new List<FieldError>();
        if (!categoryActive)
        {
            errors.Add(new FieldError("categoryId", CatalogErrors.ValidationFailed, "Move the product to an active category first."));
        }

        if (HsnCode is null)
        {
            errors.Add(new FieldError("hsnCode", CatalogErrors.ValidationFailed, "Enter the HSN code (4 to 8 digits)."));
        }

        if (TaxCategoryCode is null)
        {
            errors.Add(new FieldError("taxCategoryCode", CatalogErrors.ValidationFailed, "Choose the tax category."));
        }

        if (!_images.Any(i => !string.IsNullOrWhiteSpace(i.AltText)))
        {
            errors.Add(new FieldError("images", CatalogErrors.ValidationFailed, "Upload at least one image with alt text."));
        }

        if (!_variants.Any(v => v.IsActive))
        {
            errors.Add(new FieldError("variants", CatalogErrors.ValidationFailed, "Add at least one active variant."));
        }

        for (var i = 0; i < _variants.Count; i++)
        {
            if (_variants[i].IsActive)
            {
                VariantRules.Validate(_variants[i].ToSpec(methodCode), $"variants[{i}].", errors);
            }
        }

        ThrowIfAny(errors);
        Status = ProductStatus.Active;
        PublishedAt ??= at;
        Raise(new ProductPublished(Id, Slug));
    }

    /// <summary>DRAFT or ACTIVE → ARCHIVED.</summary>
    public void Archive()
    {
        if (Status == ProductStatus.Archived)
        {
            throw new DomainException(CatalogErrors.InvalidStatusTransition, "The product is already archived.");
        }

        Status = ProductStatus.Archived;
        Raise(new ProductArchived(Id));
    }

    /// <summary>ARCHIVED → DRAFT (catalog.md Q4); ACTIVE → DRAFT is refused (archive instead).</summary>
    public void Restore()
    {
        if (Status != ProductStatus.Archived)
        {
            throw new DomainException(CatalogErrors.InvalidStatusTransition, "Only an archived product can be restored to DRAFT; archive an active product instead.");
        }

        Status = ProductStatus.Draft;
        Raise(new ProductUpdated(Id, ["status"]));
    }

    /// <summary>Adds an active variant (CAT-R4 to CAT-R8); <paramref name="skuTaken"/> is the caller's uniqueness check.</summary>
    public ProductVariant AddVariant(string? sku, VariantSpec spec, bool skuTaken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        EnsureNotArchived();
        var errors = new List<FieldError>();
        var normalizedSku = sku?.Trim().ToUpperInvariant();
        if (!IsSku(normalizedSku))
        {
            errors.Add(new FieldError("sku", CatalogErrors.ValidationFailed, "Enter a SKU of upper-case letters, digits, '_' and '-', at most 40 characters."));
        }

        var units = VariantRules.Validate(spec, string.Empty, errors);
        ThrowIfAny(errors);
        if (skuTaken || _variants.Any(v => v.Sku == normalizedSku))
        {
            throw new ConflictException(CatalogErrors.SkuTaken, "Another variant already uses this SKU.", "sku");
        }

        var variant = new ProductVariant(Id, normalizedSku!);
        variant.Apply(spec, units, isActive: true);
        _variants.Add(variant);
        Raise(new VariantActivated(variant.Id, Id));
        Raise(PreparationsChanged(variant, spec));
        return variant;
    }

    /// <summary>Updates a variant and replaces its preparations; an ACTIVE product keeps ≥1 active variant (CAT-R3).</summary>
    public ProductVariant UpdateVariant(Guid variantId, VariantSpec spec, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(spec);
        EnsureNotArchived();
        var variant = _variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new NotFoundException(CatalogErrors.VariantNotFound, "The product has no variant with this id.");
        var errors = new List<FieldError>();
        if (variant.SellUnit != spec.SellUnit && variant.IsActive && Status == ProductStatus.Active)
        {
            errors.Add(new FieldError("sellUnit", CatalogErrors.ValidationFailed, "The sell unit of a variant of a published product cannot change; add a new variant instead."));
        }

        if (!isActive && variant.IsActive && Status == ProductStatus.Active && _variants.Count(v => v.IsActive) == 1)
        {
            errors.Add(new FieldError("isActive", CatalogErrors.ValidationFailed, "A published product keeps at least one active variant; archive the product instead."));
        }

        var units = VariantRules.Validate(spec, string.Empty, errors);
        ThrowIfAny(errors);
        var wasActive = variant.IsActive;
        var preparationsChanged = variant.Apply(spec, units, isActive);
        if (wasActive != isActive)
        {
            Raise(isActive ? new VariantActivated(variant.Id, Id) : new VariantDeactivated(variant.Id, Id));
        }

        if (preparationsChanged)
        {
            Raise(PreparationsChanged(variant, spec));
        }

        Raise(new ProductUpdated(Id, ["variants"]));
        return variant;
    }

    /// <summary>Adds an image record after its files were stored (CAT-R16, CAT-R17); it goes last.</summary>
    public ProductImage AddImage(Guid imageId, string? altText, int width, int height)
    {
        EnsureNotArchived();
        if (string.IsNullOrWhiteSpace(altText) || altText.Trim().Length > ProductImage.AltTextMaxLength)
        {
            throw new RefusalException(new FieldError("altText", CatalogErrors.ImageInvalid, $"Enter alt text of 1 to {ProductImage.AltTextMaxLength} characters."));
        }

        if (_images.Count >= MaxImages)
        {
            throw new RefusalException(new FieldError("file", CatalogErrors.ImageInvalid, $"A product has at most {MaxImages} images; remove one first."));
        }

        var image = new ProductImage(imageId, Id, altText.Trim(), width, height, _images.Count == 0 ? 0 : _images.Max(i => i.SortOrder) + 1);
        _images.Add(image);
        Raise(new ProductUpdated(Id, ["images"]));
        return image;
    }

    /// <summary>Reorders images; <paramref name="imageIds"/> must be exactly the current set.</summary>
    public void ReorderImages(IReadOnlyList<Guid> imageIds)
    {
        ArgumentNullException.ThrowIfNull(imageIds);
        if (imageIds.Count != _images.Count || imageIds.Distinct().Count() != imageIds.Count || imageIds.Any(id => _images.All(i => i.Id != id)))
        {
            throw new RefusalException(new FieldError("imageIds", CatalogErrors.ValidationFailed, "List every image of the product exactly once."));
        }

        for (var i = 0; i < imageIds.Count; i++)
        {
            _images.First(image => image.Id == imageIds[i]).SortOrder = i;
        }

        Raise(new ProductUpdated(Id, ["images"]));
    }

    /// <summary>Removes an image record; the files are deleted by the follow-on handler (CAT-R17).</summary>
    public void RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new NotFoundException(CatalogErrors.ImageNotFound, "The product has no image with this id.");
        if (Status == ProductStatus.Active && _images.Count == 1)
        {
            throw new RefusalException(new FieldError("imageId", CatalogErrors.ValidationFailed, "A published product keeps at least one image."));
        }

        _images.Remove(image);
        Raise(new ProductImageRemoved(Id, image.Id, image.StorageKey));
    }

    public ProductImage? PrimaryImage => _images.OrderBy(i => i.SortOrder).FirstOrDefault();

    public static bool IsSku(string? sku) => sku is { Length: > 0 and <= 40 } && SkuPattern().IsMatch(sku);

    public static bool IsHsnCode(string? code) => code is not null && HsnPattern().IsMatch(code);

    private static VariantPreparationsChanged PreparationsChanged(ProductVariant variant, VariantSpec spec)
        => new(variant.Id, variant.ProductId, [.. spec.Preparations.Select(p => new PreparationYield(p.MethodCode, p.YieldBp, p.IsDefault))]);

    private void EnsureNotArchived()
    {
        if (Status == ProductStatus.Archived)
        {
            throw new DomainException(CatalogErrors.InvalidStatusTransition, "An archived product cannot be changed; restore it first.");
        }
    }

    private void ApplyDetails(ProductDetails details, string slug, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(details.Name) || details.Name.Trim().Length > NameMaxLength)
        {
            errors.Add(new FieldError("name", CatalogErrors.ValidationFailed, $"Enter a name of 1 to {NameMaxLength} characters."));
        }

        if (!Slugs.IsValid(slug))
        {
            errors.Add(new FieldError("slug", CatalogErrors.ValidationFailed, "Enter a slug of lowercase letters, digits and single hyphens, at most 80 characters."));
        }

        var localNames = NormalizeLocalNames(details.LocalNames, errors);
        CheckLength(errors, "speciesScientificName", details.SpeciesScientificName, ShortTextMaxLength);
        CheckLength(errors, "description", details.Description, TextMaxLength);
        CheckLength(errors, "sourceRegion", details.SourceRegion, ShortTextMaxLength);
        CheckLength(errors, "storageInstructions", details.StorageInstructions, TextMaxLength);
        CheckLength(errors, "handlingNotes", details.HandlingNotes, TextMaxLength);
        var hsn = Normalize(details.HsnCode);
        if (hsn is not null && !IsHsnCode(hsn))
        {
            errors.Add(new FieldError("hsnCode", CatalogErrors.ValidationFailed, "Enter the HSN code as 4 to 8 digits."));
        }

        var taxCategory = Normalize(details.TaxCategoryCode);
        if (taxCategory is not null && (taxCategory.Length > 40 || !ErrorCodes.IsUpperSnake(taxCategory)))
        {
            errors.Add(new FieldError("taxCategoryCode", CatalogErrors.ValidationFailed, "Enter the tax category as an UPPER_SNAKE code."));
        }

        if (!Enum.IsDefined(details.SourceType))
        {
            errors.Add(new FieldError("sourceType", CatalogErrors.ValidationFailed, "Choose WILD or FARMED."));
        }

        if (details.FeaturedRank is < 0)
        {
            errors.Add(new FieldError("featuredRank", CatalogErrors.ValidationFailed, "Enter a featured rank of 0 or more, or leave it empty."));
        }

        if (errors.Count > 0)
        {
            return;
        }

        Slug = slug;
        Name = details.Name!.Trim();
        LocalNames = localNames;
        SpeciesScientificName = Normalize(details.SpeciesScientificName);
        Description = Normalize(details.Description);
        SourceType = details.SourceType;
        SourceRegion = Normalize(details.SourceRegion);
        StorageInstructions = Normalize(details.StorageInstructions);
        HandlingNotes = Normalize(details.HandlingNotes);
        HsnCode = hsn;
        TaxCategoryCode = taxCategory;
        FeaturedRank = details.FeaturedRank;
    }

    private static string[] NormalizeLocalNames(IReadOnlyList<string>? names, List<FieldError> errors)
    {
        var result = new List<string>();
        foreach (var name in names ?? [])
        {
            var trimmed = name?.Trim();
            if (string.IsNullOrEmpty(trimmed) || result.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (trimmed.Length > LocalNameMaxLength)
            {
                errors.Add(new FieldError("localNames", CatalogErrors.ValidationFailed, $"Each local name has at most {LocalNameMaxLength} characters."));
                return [];
            }

            result.Add(trimmed);
        }

        if (result.Count > MaxLocalNames)
        {
            errors.Add(new FieldError("localNames", CatalogErrors.ValidationFailed, $"Enter at most {MaxLocalNames} local names."));
        }

        return [.. result];
    }

    private static void CheckLength(List<FieldError> errors, string field, string? value, int max)
    {
        if (value is not null && value.Trim().Length > max)
        {
            errors.Add(new FieldError(field, CatalogErrors.ValidationFailed, $"Enter at most {max} characters."));
        }
    }

    private object[] Snapshot() =>
    [
        CategoryId, Slug, Name, string.Join('\u001f', LocalNames), SpeciesScientificName ?? string.Empty, Description ?? string.Empty, SourceType,
        SourceRegion ?? string.Empty, StorageInstructions ?? string.Empty, HandlingNotes ?? string.Empty, HsnCode ?? string.Empty,
        TaxCategoryCode ?? string.Empty, FeaturedRank ?? -1,
    ];

    private static List<string> Changes(object[] before, object[] after)
    {
        string[] names =
        [
            "categoryId", "slug", "name", "localNames", "speciesScientificName", "description", "sourceType", "sourceRegion",
            "storageInstructions", "handlingNotes", "hsnCode", "taxCategoryCode", "featuredRank",
        ];
        return [.. names.Where((_, i) => !Equals(before[i], after[i]))];
    }

    private static void ThrowIfAny(List<FieldError> errors)
    {
        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[A-Z0-9]+([_-][A-Z0-9]+)*$")]
    private static partial Regex SkuPattern();

    [GeneratedRegex("^[0-9]{4,8}$")]
    private static partial Regex HsnPattern();
}

public interface IProductRepository
{
    /// <summary>The product with variants, preparations and images.</summary>
    Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, Guid? excludingId, CancellationToken cancellationToken);

    Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken);

    void Add(Product product);

    /// <summary>Marks the product row changed so a child-only change still checks and bumps its version.</summary>
    void MarkChanged(Product product);
}
