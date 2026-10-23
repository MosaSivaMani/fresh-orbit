using FluentValidation;
using FreshOrbit.Settings.Application.BusinessProfiles;
using FreshOrbit.Settings.Application.Numbering;
using FreshOrbit.Settings.Application.OrderPolicies;
using FreshOrbit.Settings.Domain.Shared;
using FreshOrbit.Settings.Infrastructure.Endpoints;
using FreshOrbit.Settings.Infrastructure.Persistence;
using FreshOrbit.Settings.Infrastructure.Persistence.Repositories;
using FreshOrbit.Settings.Infrastructure.Public;
using FreshOrbit.Settings.Infrastructure.Seeding;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Settings.Infrastructure;

/// <summary>Composition root of the Settings module.</summary>
public sealed class SettingsModule : IModule
{
    public string Name => "Settings";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<SettingsModuleModel>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IBusinessProfileService, BusinessProfileService>();
        services.AddScoped<IOrderPolicyService, OrderPolicyService>();
        services.AddScoped<INumberSeriesService, NumberSeriesService>();
        services.AddScoped<ISettingsApi, SettingsApi>();
        services.AddScoped<IValidator<SaveBusinessProfileRequest>, SaveBusinessProfileRequestValidator>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IReferenceDataLoader, SettingsReferenceDataLoader>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => SettingsEndpoints.Map(endpoints);
}
