using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.SharedKernel.Web.Modules;

/// <summary>
/// A module's composition root. <see cref="Register"/> runs in every host (Api, Worker, Migrations) and must only
/// register services; <see cref="MapEndpoints"/> runs in the Api with a route group rooted at <c>/api/v1</c>
/// (customer endpoints at <c>/…</c>, staff endpoints under <c>/admin/…</c>).
/// </summary>
public interface IModule
{
    string Name { get; }

    void Register(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
