using FluentValidation;
using FreshOrbit.Catalog.Application.Admin;
using FreshOrbit.Catalog.Application.Storefront;
using FreshOrbit.Catalog.Domain.Categories;
using FreshOrbit.Catalog.Domain.Preparations;
using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.Catalog.Domain.Products.Events;
using FreshOrbit.Catalog.Infrastructure.Endpoints;
using FreshOrbit.Catalog.Infrastructure.Images;
using FreshOrbit.Catalog.Infrastructure.Persistence;
using FreshOrbit.Catalog.Infrastructure.Persistence.Repositories;
using FreshOrbit.Catalog.Infrastructure.Public;
using FreshOrbit.Catalog.Infrastructure.Reads;
using FreshOrbit.Catalog.Infrastructure.Seeding;
using FreshOrbit.Catalog.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Web.Modules;
using FreshOrbit.SharedKernel.Web.Paging;
using FreshOrbit.SharedKernel.Web.Storage;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Catalog.Infrastructure;

/// <summary>Composition root of the Catalog module.</summary>
public sealed class CatalogModule : IModule
{
    public string Name => "Catalog";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<CatalogModuleModel>();
        services.AddFreshOrbitFileStorage();
        services.TryAddScoped<IValidator<PagedRequest>, PagedRequestValidator>();

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IPreparationMethodRepository, PreparationMethodRepository>();
        services.AddScoped<ICatalogAdminReads, CatalogAdminReads>();
        services.AddScoped<IStorefrontReads, StorefrontReads>();
        services.AddScoped<CategoryService>();
        services.AddScoped<ProductService>();
        services.AddScoped<ProductImageService>();
        services.AddScoped<StorefrontService>();
        services.TryAddSingleton<SkiaImageProcessor>();
        services.TryAddSingleton<IImageProcessor>(sp => sp.GetRequiredService<SkiaImageProcessor>());
        services.TryAddSingleton<IImageSanitizer>(sp => sp.GetRequiredService<SkiaImageProcessor>());
        services.AddScoped<ICatalogApi, CatalogApi>();

        services.AddEventHandler<ProductImageRemoved, ProductImageRemovedHandler>();
        services.AddScoped<OrphanImageCleanupJob>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRecurringJobRegistration, CatalogRecurringJobs>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IReferenceDataLoader, PreparationMethodsLoader>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISampleDataLoader, CatalogSampleDataLoader>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => CatalogEndpoints.Map(endpoints);
}
