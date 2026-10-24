using Microsoft.Extensions.DependencyInjection.Extensions;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.Customers.Infrastructure.Seeding;
using FluentValidation;
using FreshOrbit.Customers.Application.Addresses;
using FreshOrbit.Customers.Application.Admin;
using FreshOrbit.Customers.Application.Profiles;
using FreshOrbit.Customers.Application.Shared;
using FreshOrbit.Customers.Application.Wishlist;
using FreshOrbit.Customers.Application.StockAlerts;
using FreshOrbit.Customers.Application.Handlers;
using FreshOrbit.Inventory.Public.Events;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.Customers.Infrastructure.Endpoints;
using FreshOrbit.Customers.Infrastructure.Persistence;
using FreshOrbit.Customers.Infrastructure.Persistence.Repositories;
using FreshOrbit.Customers.Infrastructure.Public;
using FreshOrbit.Customers.Public;
using FreshOrbit.Identity.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Customers.Infrastructure;

/// <summary>Composition root of the Customers module.</summary>
public sealed class CustomersModule : IModule
{
    public string Name => "Customers";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<CustomersModuleModel>();
        services.AddScoped<ICustomersRepository, CustomersRepository>();
        services.AddScoped<CustomerContext>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<IWishlistService, WishlistService>();
        services.AddScoped<IStockAlertService, StockAlertService>();
        services.AddScoped<StockAlertTrigger>();
        services.AddEventHandler<StockBackInStock, CustomersStockBackInStockHandler>();
        services.AddScoped<IAdminCustomerService, AdminCustomerService>();
        services.AddScoped<ICustomersApi, CustomersApi>();
        services.AddScoped<ICustomerAccountDirectory, CustomerAccountDirectory>();
        services.AddScoped<IValidator<UpdateProfileRequest>, UpdateProfileRequestValidator>();
        services.AddScoped<IValidator<CustomerListRequest>, CustomerListRequestValidator>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISampleDataLoader, CustomersSampleDataLoader>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => CustomersEndpoints.Map(endpoints);
}
