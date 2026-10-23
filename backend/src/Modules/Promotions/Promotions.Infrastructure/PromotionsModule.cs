using FreshOrbit.Promotions.Application.Abstractions;
using FreshOrbit.Promotions.Application.Banners;
using FreshOrbit.Promotions.Application.Coupons;
using FreshOrbit.Promotions.Application.Handlers;
using FreshOrbit.Promotions.Infrastructure.Endpoints;
using FreshOrbit.Promotions.Infrastructure.Jobs;
using FreshOrbit.Promotions.Infrastructure.Persistence;
using FreshOrbit.Promotions.Infrastructure.Public;
using FreshOrbit.Promotions.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Promotions.Infrastructure;

/// <summary>Composition root of the Promotions module.</summary>
public sealed class PromotionsModule : IModule
{
    public string Name => "Promotions";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<PromotionsModuleModel>();

        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<IRedemptionRepository, RedemptionRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IPromotionsReads, PromotionsReads>();
        services.TryAddScoped<IPromotionsCatalog, CatalogGapPromotionsCatalog>();
        services.TryAddScoped<ICityDirectory, LocationsGapCityDirectory>();

        services.AddScoped<CouponRedemptionService>();
        services.AddScoped<CouponAdminService>();
        services.AddScoped<BannerService>();
        services.AddScoped<RedemptionReversalService>();
        services.AddScoped<IPromotionsApi, PromotionsApi>();

        services.AddScoped<ExpireCouponsJob>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRecurringJobRegistration, PromotionsJobs>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => PromotionsEndpoints.Map(endpoints);
}
