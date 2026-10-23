using FreshOrbit.Promotions.Infrastructure.Persistence;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Promotions.Infrastructure;

/// <summary>Composition root of the Promotions module.</summary>
public sealed class PromotionsModule : IModule
{
    public string Name => "Promotions";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<PromotionsModuleModel>();

        // Owner: register the Public API implementation, services, repositories, validators,
        // event handlers, recurring jobs and data loaders of this module here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Owner: map customer endpoints at /api/v1/... and staff endpoints under /api/v1/admin/...
    }
}
