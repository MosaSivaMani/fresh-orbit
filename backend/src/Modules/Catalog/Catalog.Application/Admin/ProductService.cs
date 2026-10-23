using FreshOrbit.Catalog.Domain.Categories;
using FreshOrbit.Catalog.Domain.Preparations;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Storage;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Catalog.Application.Admin;

/// <summary>Product commands (catalog.md §5): fields, status, variants and image order/removal.</summary>
public sealed class ProductService(
    IProductRepository products,
    ICategoryRepository categories,
    IPreparationMethodRepository preparationMethods,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<AdminProductResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await LoadAsync(id, cancellationToken);
        return await MapAsync(product, cancellationToken);
    }

    public async Task<AdminProductResponse> CreateAsync(ProductRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureCategoryAsync(request.CategoryId, cancellationToken);
        var product = Product.Create(request.CategoryId, ToDetails(request));
        await EnsureSlugFreeAsync(product.Slug, null, cancellationToken);
        products.Add(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(product, cancellationToken);
    }

    public async Task<AdminProductResponse> UpdateAsync(Guid id, uint expectedVersion, ProductRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await LoadAsync(id, expectedVersion, cancellationToken);
        if (request.CategoryId != product.CategoryId)
        {
            await EnsureCategoryAsync(request.CategoryId, cancellationToken);
        }

        product.Update(request.CategoryId, ToDetails(request));
        await EnsureSlugFreeAsync(product.Slug, product.Id, cancellationToken);
        return await SaveAsync(product, cancellationToken);
    }

    public async Task<AdminProductResponse> PublishAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken)
    {
        var product = await LoadAsync(id, expectedVersion, cancellationToken);
        var category = await categories.GetAsync(product.CategoryId, cancellationToken);
        var parent = category?.ParentId is { } parentId ? await categories.GetAsync(parentId, cancellationToken) : null;
        var categoryActive = category is { IsActive: true } && (category.ParentId is null || parent is { IsActive: true });
        var methods = await MethodsAsync(cancellationToken);
        product.Publish(categoryActive, methodId => methods.TryGetValue(methodId, out var method) ? method.Code : string.Empty, clock.UtcNow);
        return await SaveAsync(product, cancellationToken);
    }

    public async Task<AdminProductResponse> ArchiveAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken)
    {
        var product = await LoadAsync(id, expectedVersion, cancellationToken);
        product.Archive();
        return await SaveAsync(product, cancellationToken);
    }

    public async Task<AdminProductResponse> RestoreAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken)
    {
        var product = await LoadAsync(id, expectedVersion, cancellationToken);
        product.Restore();
        return await SaveAsync(product, cancellationToken);
    }

    public async Task<AdminProductResponse> AddVariantAsync(Guid id, uint expectedVersion, VariantRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await LoadAsync(id, expectedVersion, cancellationToken);
        var spec = await ToSpecAsync(request, cancellationToken);
        var sku = request.Sku?.Trim().ToUpperInvariant();
        var skuTaken = Product.IsSku(sku) && await products.SkuExistsAsync(sku!, cancellationToken);
        product.AddVariant(sku, spec, skuTaken);
        return await SaveAsync(product, cancellationToken);
    }

    public async Task<AdminProductResponse> UpdateVariantAsync(Guid id, Guid variantId, uint expectedVersion, VariantRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await LoadAsync(id, expectedVersion, cancellationToken);
        var spec = await ToSpecAsync(request, cancellationToken);
        product.UpdateVariant(variantId, spec, request.IsActive ?? true);
        return await SaveAsync(product, cancellationToken);
    }

    public async Task<AdminProductResponse> ReorderImagesAsync(Guid id, uint expectedVersion, ReorderImagesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await LoadAsync(id, expectedVersion, cancellationToken);
        product.ReorderImages(request.ImageIds ?? []);
        return await SaveAsync(product, cancellationToken);
    }

    public async Task<AdminProductResponse> RemoveImageAsync(Guid id, Guid imageId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var product = await LoadAsync(id, expectedVersion, cancellationToken);
        product.RemoveImage(imageId);
        return await SaveAsync(product, cancellationToken);
    }

    internal async Task<Product> LoadAsync(Guid id, CancellationToken cancellationToken)
        => await products.GetAsync(id, cancellationToken) ?? throw new NotFoundException(CatalogErrors.ProductNotFound, "No product has this id.");

    internal async Task<AdminProductResponse> MapAsync(Product product, CancellationToken cancellationToken)
    {
        var methods = await MethodsAsync(cancellationToken);
        return new AdminProductResponse(
            product.Id,
            product.CategoryId,
            product.Slug,
            product.Name,
            product.LocalNames,
            product.SpeciesScientificName,
            product.Description,
            product.SourceType,
            product.SourceRegion,
            product.StorageInstructions,
            product.HandlingNotes,
            product.HsnCode,
            product.TaxCategoryCode,
            product.Status,
            product.FeaturedRank,
            product.PublishedAt,
            CatalogFormats.Version(product.Version),
            [.. product.Variants.Select(v => new AdminVariantResponse(
                v.Id,
                v.Sku,
                v.Name,
                v.SellUnit,
                v.StockUnit,
                v.PackSizeGrams,
                v.AvgPieceGrams,
                v.MinOrder,
                v.MaxOrder,
                v.OrderStep,
                v.IsWeightVariable,
                v.IsActive,
                [.. v.Preparations
                    .Select(p => (Preparation: p, Method: methods.GetValueOrDefault(p.PreparationMethodId)))
                    .OrderBy(x => x.Method?.SortOrder ?? int.MaxValue)
                    .Select(x => new AdminPreparationResponse(x.Method?.Code ?? string.Empty, x.Method?.Name ?? string.Empty, x.Preparation.YieldBp, x.Preparation.IsDefault, x.Preparation.IsActive))]))],
            [.. product.Images.OrderBy(i => i.SortOrder).Select(i => new AdminImageResponse(
                i.Id,
                i.StorageKey,
                storage.GetPublicUrl(ImageRenditions.Key(i.StorageKey, ImageRenditions.Medium)),
                i.AltText,
                i.Width,
                i.Height,
                i.SortOrder))]);
    }

    private async Task<Product> LoadAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken)
    {
        var product = await LoadAsync(id, cancellationToken);
        unitOfWork.SetExpectedVersion(product, expectedVersion);
        return product;
    }

    private async Task<AdminProductResponse> SaveAsync(Product product, CancellationToken cancellationToken)
    {
        products.MarkChanged(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(product, cancellationToken);
    }

    private async Task EnsureCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (await categories.GetAsync(categoryId, cancellationToken) is null)
        {
            throw new RefusalException(new FieldError("categoryId", CatalogErrors.ValidationFailed, "Choose an existing category."));
        }
    }

    private async Task EnsureSlugFreeAsync(string slug, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (await products.SlugExistsAsync(slug, excludingId, cancellationToken))
        {
            throw new ConflictException(CatalogErrors.SlugTaken, "Another product already uses this slug.", "slug");
        }
    }

    private async Task<Dictionary<Guid, PreparationMethod>> MethodsAsync(CancellationToken cancellationToken)
        => (await preparationMethods.ListAsync(cancellationToken)).ToDictionary(m => m.Id);

    private async Task<VariantSpec> ToSpecAsync(VariantRequest request, CancellationToken cancellationToken)
    {
        var methods = (await preparationMethods.ListAsync(cancellationToken)).ToDictionary(m => m.Code, StringComparer.Ordinal);
        var errors = new List<FieldError>();
        var preparations = new List<PreparationSpec>();
        var requested = request.Preparations ?? [];
        for (var i = 0; i < requested.Count; i++)
        {
            var item = requested[i];
            var code = item?.Code?.Trim().ToUpperInvariant();
            if (item is null || code is null || !methods.TryGetValue(code, out var method))
            {
                errors.Add(new FieldError($"preparations[{i}].code", CatalogErrors.ValidationFailed, "Choose a preparation method from the reference list."));
                continue;
            }

            preparations.Add(new PreparationSpec(method.Id, method.Code, item.YieldBp, item.IsDefault));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        return new VariantSpec(request.Name, request.SellUnit, request.PackSizeGrams, request.AvgPieceGrams, request.MinOrder, request.MaxOrder, request.OrderStep, preparations);
    }

    private static ProductDetails ToDetails(ProductRequest request) => new(
        request.Slug,
        request.Name,
        request.LocalNames,
        request.SpeciesScientificName,
        request.Description,
        request.SourceType,
        request.SourceRegion,
        request.StorageInstructions,
        request.HandlingNotes,
        request.HsnCode,
        request.TaxCategoryCode,
        request.FeaturedRank);
}
