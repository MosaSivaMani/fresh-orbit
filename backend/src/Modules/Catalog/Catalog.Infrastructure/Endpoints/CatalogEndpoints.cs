using FreshOrbit.Catalog.Application.Admin;
using FreshOrbit.Catalog.Application.Storefront;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.Paging;
using FreshOrbit.SharedKernel.Web.RateLimiting;
using FreshOrbit.SharedKernel.Web.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FreshOrbit.Catalog.Infrastructure.Endpoints;

/// <summary>docs/api/README.md §4.4 (public catalog and search), §4.5 <c>GET /availability</c> (D3) and §4.11 (admin).</summary>
internal static class CatalogEndpoints
{
    public const string ManagePermission = "catalog.manage";
    public const string PincodeHeader = "X-Pincode";
    public const string LocationHeader = "X-Fulfillment-Location";
    public const string ServiceAreaHeader = "X-Service-Area";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        MapStorefront(endpoints.MapGroup(string.Empty).AddEndpointFilter<CatalogProblemFilter>().WithTags("Catalog"));
        var admin = endpoints.MapGroup("/admin").AddEndpointFilter<CatalogProblemFilter>().WithTags("Admin catalog");
        MapAdminCategories(admin);
        MapAdminProducts(admin);
    }

    private static void MapStorefront(RouteGroupBuilder group)
    {
        group.MapGet("/home", async (HttpContext http, StorefrontService service, IStorefrontReads reads, IClock clock, CancellationToken ct) =>
        {
            var location = await ResolveAsync(http, service, required: false, ct);
            var categories = await reads.GetCategoryTreeAsync(ct);
            var query = new ProductListQuery([], null, [], [], null, null, null, ProductSort.Featured, 0, 12) { FeaturedOnly = true };
            var (products, _) = await reads.ListProductsAsync(query, location, clock.UtcNow, ct);
            return TypedResults.Ok(new HomeResponse([], categories, products, location is null));
        }).RequireRateLimiting(RateLimitPolicies.Public);

        group.MapGet("/categories", async (IStorefrontReads reads, CancellationToken ct) => TypedResults.Ok(await reads.GetCategoryTreeAsync(ct)))
            .RequireRateLimiting(RateLimitPolicies.Public);

        group.MapGet("/categories/{slug}", async (string slug, IStorefrontReads reads, CancellationToken ct) =>
        {
            var tree = await reads.GetCategoryTreeAsync(ct);
            var parent = tree.FirstOrDefault(c => c.Children.Any(x => x.Slug == slug));
            var category = tree.FirstOrDefault(c => c.Slug == slug) ?? parent?.Children.First(c => c.Slug == slug)
                ?? throw new NotFoundException(CatalogErrors.CategoryNotFound, "No category has this slug.");
            return TypedResults.Ok(new StorefrontCategoryDetail(
                category.Id,
                category.Slug,
                category.Name,
                category.Description,
                category.ImageUrl,
                parent is null ? null : new CategoryRef(parent.Slug, parent.Name),
                category.Children));
        }).RequireRateLimiting(RateLimitPolicies.Public);

        group.MapGet("/products", async (
                HttpContext http,
                [AsParameters] PagedRequest paging,
                [FromQuery(Name = "category")] string[]? category,
                string? q,
                [FromQuery(Name = "sourceType")] string[]? sourceType,
                [FromQuery(Name = "preparation")] string[]? preparation,
                bool? inStock,
                long? minPricePaise,
                long? maxPricePaise,
                string? sort,
                StorefrontService service,
                IStorefrontReads reads,
                IClock clock,
                CancellationToken ct) =>
            {
                var location = await ResolveAsync(http, service, required: false, ct);
                var query = StorefrontService.BuildQuery(category, q, sourceType, preparation, inStock, minPricePaise, maxPricePaise, sort, paging.Skip, paging.PageSize, location is not null);
                var (items, total) = await reads.ListProductsAsync(query, location, clock.UtcNow, ct);
                return TypedResults.Ok(new PagedResponse<ProductListItem>(items, paging.Page, paging.PageSize, total));
            })
            .WithValidation<PagedRequest>()
            .RequireRateLimiting(RateLimitPolicies.Search);

        group.MapGet("/products/{slug}", async (string slug, string? preparation, HttpContext http, StorefrontService service, IStorefrontReads reads, IClock clock, CancellationToken ct) =>
        {
            var location = await ResolveAsync(http, service, required: false, ct);
            var product = await reads.GetProductAsync(slug, location, preparation?.Trim().ToUpperInvariant(), clock.UtcNow, ct)
                ?? throw new NotFoundException(CatalogErrors.ProductNotFound, "No product has this slug.");
            return TypedResults.Ok(product);
        }).RequireRateLimiting(RateLimitPolicies.Public);

        group.MapGet("/search/suggest", async (string? q, HttpContext http, StorefrontService service, IStorefrontReads reads, IClock clock, CancellationToken ct) =>
        {
            var query = StorefrontService.BuildSuggestQuery(q);
            var location = await ResolveAsync(http, service, required: false, ct);
            return TypedResults.Ok(await reads.SuggestAsync(query, location, clock.UtcNow, ct));
        }).RequireRateLimiting(RateLimitPolicies.Search);

        group.MapGet("/availability", async ([FromQuery(Name = "variantId")] Guid[]? variantId, string? preparation, HttpContext http, StorefrontService service, IStorefrontReads reads, IClock clock, CancellationToken ct) =>
        {
            if (variantId is not { Length: > 0 and <= 50 })
            {
                throw new RefusalException(new FieldError("variantId", CatalogErrors.ValidationFailed, "Send 1 to 50 variantId values."));
            }

            var location = (await ResolveAsync(http, service, required: true, ct))!;
            var items = await reads.GetAvailabilityAsync(location, variantId, preparation?.Trim().ToUpperInvariant(), clock.UtcNow, ct);
            return TypedResults.Ok(new AvailabilityResponse(new LocationRef(location.FulfillmentLocationCode, location.Pincode), items));
        }).RequireRateLimiting(RateLimitPolicies.Public);
    }

    private static void MapAdminCategories(RouteGroupBuilder admin)
    {
        admin.MapGet("/categories", async (bool? includeInactive, ICatalogAdminReads reads, CancellationToken ct) =>
                TypedResults.Ok(await reads.GetCategoryTreeAsync(includeInactive ?? true, ct)))
            .RequirePermission(ManagePermission);

        admin.MapGet("/categories/{id:guid}", async (Guid id, ICatalogAdminReads reads, HttpResponse response, CancellationToken ct) =>
            {
                var category = await reads.GetCategoryAsync(id, ct) ?? throw new NotFoundException(CatalogErrors.CategoryNotFound, "No category has this id.");
                return Respond(response, category, category.Version);
            })
            .RequirePermission(ManagePermission);

        admin.MapPost("/categories", async (CreateCategoryRequest request, CategoryService service, HttpResponse response, CancellationToken ct) =>
            {
                var category = await service.CreateAsync(request, ct);
                SetETag(response, category.Version);
                return TypedResults.Created($"/api/v1/admin/categories/{category.Id}", category);
            })
            .RequirePermission(ManagePermission);

        admin.MapPut("/categories/{id:guid}", async (Guid id, UpdateCategoryRequest request, CategoryService service, HttpContext http, CancellationToken ct) =>
            {
                var category = await service.UpdateAsync(id, IfMatch.GetRequiredVersion(http.Request), request, ct);
                return Respond(http.Response, category, category.Version);
            })
            .RequirePermission(ManagePermission);
    }

    private static void MapAdminProducts(RouteGroupBuilder admin)
    {
        admin.MapGet("/preparation-methods", async (ICatalogAdminReads reads, CancellationToken ct) => TypedResults.Ok(await reads.ListPreparationMethodsAsync(ct)))
            .RequirePermission(ManagePermission);

        admin.MapGet("/products", async (
                [AsParameters] PagedRequest paging,
                string? q,
                Guid? categoryId,
                [FromQuery(Name = "status")] ProductStatus[]? status,
                ICatalogAdminReads reads,
                CancellationToken ct) =>
            {
                var (items, total) = await reads.ListProductsAsync(q, categoryId, status ?? [], paging.Skip, paging.PageSize, ct);
                return TypedResults.Ok(new PagedResponse<AdminProductListItem>(items, paging.Page, paging.PageSize, total));
            })
            .WithValidation<PagedRequest>()
            .RequirePermission(ManagePermission);

        admin.MapPost("/products", async (ProductRequest request, ProductService service, HttpResponse response, CancellationToken ct) =>
            {
                var product = await service.CreateAsync(request, ct);
                SetETag(response, product.Version);
                return TypedResults.Created($"/api/v1/admin/products/{product.Id}", product);
            })
            .RequirePermission(ManagePermission);

        admin.MapGet("/products/{id:guid}", async (Guid id, ProductService service, HttpResponse response, CancellationToken ct) =>
            {
                var product = await service.GetAsync(id, ct);
                return Respond(response, product, product.Version);
            })
            .RequirePermission(ManagePermission);

        admin.MapPut("/products/{id:guid}", async (Guid id, ProductRequest request, ProductService service, HttpContext http, CancellationToken ct) =>
                RespondProduct(http, await service.UpdateAsync(id, IfMatch.GetRequiredVersion(http.Request), request, ct)))
            .RequirePermission(ManagePermission);

        admin.MapPost("/products/{id:guid}/publish", async (Guid id, ProductService service, HttpContext http, CancellationToken ct) =>
                RespondProduct(http, await service.PublishAsync(id, IfMatch.GetRequiredVersion(http.Request), ct)))
            .RequirePermission(ManagePermission);

        admin.MapPost("/products/{id:guid}/archive", async (Guid id, ProductService service, HttpContext http, CancellationToken ct) =>
                RespondProduct(http, await service.ArchiveAsync(id, IfMatch.GetRequiredVersion(http.Request), ct)))
            .RequirePermission(ManagePermission);

        admin.MapPost("/products/{id:guid}/restore", async (Guid id, ProductService service, HttpContext http, CancellationToken ct) =>
                RespondProduct(http, await service.RestoreAsync(id, IfMatch.GetRequiredVersion(http.Request), ct)))
            .RequirePermission(ManagePermission);

        admin.MapPost("/products/{id:guid}/variants", async (Guid id, VariantRequest request, ProductService service, HttpContext http, CancellationToken ct) =>
                RespondProduct(http, await service.AddVariantAsync(id, IfMatch.GetRequiredVersion(http.Request), request, ct)))
            .RequirePermission(ManagePermission);

        admin.MapPut("/products/{id:guid}/variants/{variantId:guid}", async (Guid id, Guid variantId, VariantRequest request, ProductService service, HttpContext http, CancellationToken ct) =>
                RespondProduct(http, await service.UpdateVariantAsync(id, variantId, IfMatch.GetRequiredVersion(http.Request), request, ct)))
            .RequirePermission(ManagePermission);

        admin.MapPost("/products/{id:guid}/images", async (Guid id, IFormFile? file, [FromForm] string? altText, ProductImageService service, HttpResponse response, CancellationToken ct) =>
            {
                if (file is null || file.Length == 0)
                {
                    throw new RefusalException(new FieldError("file", CatalogErrors.ImageInvalid, "Attach the image as the multipart field 'file'."));
                }

                await using var stream = file.OpenReadStream();
                var (image, version) = await service.UploadAsync(id, stream, altText, ct);
                response.SetETag(version);
                return TypedResults.Created($"/api/v1/admin/products/{id}", image);
            })
            .DisableAntiforgery()
            .RequirePermission(ManagePermission);

        admin.MapPut("/products/{id:guid}/images/order", async (Guid id, ReorderImagesRequest request, ProductService service, HttpContext http, CancellationToken ct) =>
                RespondProduct(http, await service.ReorderImagesAsync(id, IfMatch.GetRequiredVersion(http.Request), request, ct)))
            .RequirePermission(ManagePermission);

        admin.MapDelete("/products/{id:guid}/images/{imageId:guid}", async (Guid id, Guid imageId, ProductService service, HttpContext http, CancellationToken ct) =>
                RespondProduct(http, await service.RemoveImageAsync(id, imageId, IfMatch.GetRequiredVersion(http.Request), ct)))
            .RequirePermission(ManagePermission);
    }

    /// <summary>Location context (docs/api §1.4): query <c>pincode</c> wins over the <c>X-Pincode</c> header; echoes the resolved location.</summary>
    private static async Task<StorefrontLocation?> ResolveAsync(HttpContext http, StorefrontService service, bool required, CancellationToken ct)
    {
        var pincode = http.Request.Query["pincode"].ToString();
        if (string.IsNullOrWhiteSpace(pincode))
        {
            pincode = http.Request.Headers[PincodeHeader].ToString();
        }

        var location = await service.ResolveLocationAsync(pincode, required, ct);
        if (location is not null)
        {
            http.Response.Headers[LocationHeader] = location.FulfillmentLocationCode;
            http.Response.Headers[ServiceAreaHeader] = location.ServiceAreaId.ToString("D");
        }

        return location;
    }

    private static Ok<AdminProductResponse> RespondProduct(HttpContext http, AdminProductResponse product) => Respond(http.Response, product, product.Version);

    private static Ok<T> Respond<T>(HttpResponse response, T body, string version)
    {
        SetETag(response, version);
        return TypedResults.Ok(body);
    }

    private static void SetETag(HttpResponse response, string version)
        => response.SetETag(uint.Parse(version, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture));
}
