using FreshOrbit.Cart.Application.Handlers;
using FreshOrbit.Identity.Public.Events;
using FreshOrbit.Cart.Application.Abstractions;
using FreshOrbit.Cart.Application.Carts;
using FreshOrbit.Cart.Application.Pricing;
using FreshOrbit.Cart.Infrastructure.Endpoints;
using FreshOrbit.Cart.Infrastructure.Jobs;
using FreshOrbit.Cart.Infrastructure.Persistence;
using FreshOrbit.Cart.Infrastructure.Public;
using FreshOrbit.Cart.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Cart.Infrastructure;

/// <summary>Composition root of the Cart module.</summary>
public sealed class CartModule : IModule
{
    public string Name => "Cart";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<CartModuleModel>();

        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ICartCommandRunner, CartCommandRunner>();
        services.TryAddScoped<IDeliveryFeeTaxRate, PricingDeliveryFeeTaxRate>();
        services.AddScoped<CartPricer>();
        services.AddScoped<CartService>();
        services.AddEventHandler<CustomerSignedIn, CartCustomerSignedInHandler>();
        services.AddScoped<ICartApi, CartApi>();

        services.AddScoped<AbandonCartsJob>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRecurringJobRegistration, CartJobs>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => CartEndpoints.Map(endpoints);
}
