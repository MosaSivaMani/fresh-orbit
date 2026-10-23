using FreshOrbit.Locations.Public;
using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.Ordering.Application.Checkout;
using FreshOrbit.Ordering.Application.Complaints;
using FreshOrbit.Ordering.Application.Events;
using FreshOrbit.Ordering.Application.Orders;
using FreshOrbit.Ordering.Infrastructure.Endpoints;
using FreshOrbit.Ordering.Infrastructure.Jobs;
using FreshOrbit.Ordering.Infrastructure.Persistence;
using FreshOrbit.Ordering.Infrastructure.Public;
using FreshOrbit.Ordering.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Ordering.Infrastructure;

/// <summary>Composition root of the Ordering module.</summary>
public sealed class OrderingModule : IModule
{
    public string Name => "Ordering";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<OrderingModuleModel>();

        var options = new OrderingOptions();
        configuration?.GetSection(OrderingOptions.SectionName).Bind(options);
        services.TryAddSingleton(options);

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IComplaintRepository, ComplaintRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IOrderReads, OrderReads>();
        services.AddScoped<IOrderingTransactions, OrderingTransactions>();

        // Merge gaps: replaced by adapters over Inventory / Payments / Delivery Public APIs once they exist.
        services.TryAddScoped<IOrderStockReleaser, InventoryGapStockReleaser>();
        services.TryAddScoped<IPaymentMethodAvailability, ConfiguredPaymentMethodAvailability>();
        services.TryAddScoped<IShipmentTracking, DeliveryGapShipmentTracking>();

        services.AddScoped<OrderQueryService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<SubstitutionPricer>();
        services.AddScoped<OrderCommandService>();
        services.AddScoped<ComplaintService>();
        services.AddScoped<OrderEventService>();
        services.AddScoped<OrderExpiryService>();

        services.AddScoped<IOrderingApi, OrderingApi>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICityRetirementCheck, OrderingCityRetirementCheck>());
        services.AddScoped<OrderingCustomerDeletionCheck>();

        services.AddScoped<ExpireUnpaidOrdersJob>();
        services.AddScoped<ExpireSubstitutionsJob>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRecurringJobRegistration, OrderingJobs>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => OrderingEndpoints.Map(endpoints);
}
