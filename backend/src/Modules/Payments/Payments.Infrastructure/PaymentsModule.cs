using FreshOrbit.Payments.Infrastructure.Persistence;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Payments.Infrastructure;

/// <summary>Composition root of the Payments module.</summary>
public sealed class PaymentsModule : IModule
{
    public string Name => "Payments";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<PaymentsModuleModel>();

        // Owner: register the Public API implementation, services, repositories, validators,
        // event handlers, recurring jobs and data loaders of this module here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Owner: map customer endpoints at /api/v1/... and staff endpoints under /api/v1/admin/...
    }
}
