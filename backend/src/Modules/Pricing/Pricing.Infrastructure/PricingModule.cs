using FluentValidation;
using FreshOrbit.Locations.Public;
using FreshOrbit.Pricing.Application.Schedules;
using FreshOrbit.Pricing.Domain.Periods;
using FreshOrbit.Pricing.Infrastructure.Endpoints;
using FreshOrbit.Pricing.Infrastructure.Persistence;
using FreshOrbit.Pricing.Infrastructure.Public;
using FreshOrbit.Pricing.Infrastructure.Seeding;
using FreshOrbit.Pricing.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Web.Modules;
using FreshOrbit.SharedKernel.Web.Paging;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Pricing.Infrastructure;

/// <summary>Composition root of the Pricing module.</summary>
public sealed class PricingModule : IModule
{
    public string Name => "Pricing";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<PricingModuleModel>();
        services.TryAddScoped<IValidator<PagedRequest>, PagedRequestValidator>();

        services.AddScoped<IPricingRepository, PricingRepository>();
        services.AddScoped<PricingService>();
        services.AddScoped<BulkPriceService>();
        services.AddScoped<IPricingApi, PricingApi>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICityActivationCheck, PricingCityActivationCheck>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IReferenceDataLoader, TaxCategoriesLoader>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => PricingEndpoints.Map(endpoints);
}
