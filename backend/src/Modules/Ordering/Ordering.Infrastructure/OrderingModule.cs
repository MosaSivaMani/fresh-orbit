using FreshOrbit.Ordering.Infrastructure.Persistence;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Ordering.Infrastructure;

/// <summary>Composition root of the Ordering module.</summary>
public sealed class OrderingModule : IModule
{
    public string Name => "Ordering";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<OrderingModuleModel>();

        // Owner: register the Public API implementation, services, repositories, validators,
        // event handlers, recurring jobs and data loaders of this module here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Owner: map customer endpoints at /api/v1/... and staff endpoints under /api/v1/admin/...
    }
}
