using FluentValidation;
using FreshOrbit.Locations.Application.Cities;
using FreshOrbit.Locations.Application.Common;
using FreshOrbit.Locations.Application.FulfillmentLocations;
using FreshOrbit.Locations.Application.ServiceAreas;
using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.FulfillmentLocations;
using FreshOrbit.Locations.Domain.ServiceAreas;
using FreshOrbit.Locations.Infrastructure.Endpoints;
using FreshOrbit.Locations.Infrastructure.Persistence;
using FreshOrbit.Locations.Infrastructure.Persistence.Repositories;
using FreshOrbit.Locations.Infrastructure.Public;
using FreshOrbit.Locations.Infrastructure.Reads;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Web.Modules;
using FreshOrbit.SharedKernel.Web.Paging;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Locations.Infrastructure;

/// <summary>Composition root of the Locations module.</summary>
public sealed class LocationsModule : IModule
{
    public string Name => "Locations";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<LocationsModuleModel>();
        services.AddMemoryCache();
        services.TryAddScoped<IValidator<PagedRequest>, PagedRequestValidator>();

        services.AddSingleton<PincodeResolutionCache>();
        services.AddSingleton<IPincodeResolutionCache>(provider => provider.GetRequiredService<PincodeResolutionCache>());
        services.AddScoped<ICityRepository, CityRepository>();
        services.AddScoped<IFulfillmentLocationRepository, FulfillmentLocationRepository>();
        services.AddScoped<IServiceAreaRepository, ServiceAreaRepository>();
        services.AddScoped<ILocationsReads, LocationsReads>();
        services.AddScoped<CityService>();
        services.AddScoped<FulfillmentLocationService>();
        services.AddScoped<ServiceAreaService>();
        services.AddScoped<LocationsApi>();
        services.AddScoped<ILocationsApi>(provider => provider.GetRequiredService<LocationsApi>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => LocationsEndpoints.Map(endpoints);
}
