using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.Ordering.Infrastructure.Seeding;
using FreshOrbit.Ordering.Application.Handlers;
using FreshOrbit.Delivery.Public.Events;
using FreshOrbit.Payments.Public.Events;
using FreshOrbit.Customers.Public;
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
using FreshOrbit.SharedKernel.Web.Storage;
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
        services.AddFreshOrbitFileStorage();

        var options = new OrderingOptions();
        configuration?.GetSection(OrderingOptions.SectionName).Bind(options);
        services.TryAddSingleton(options);

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IComplaintRepository, ComplaintRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IOrderReads, OrderReads>();
        services.AddScoped<IOrderingTransactions, OrderingTransactions>();

        // Ports over other modules' Public APIs.
        services.TryAddScoped<IOrderStockReleaser, InventoryOrderStockReleaser>();
        services.TryAddScoped<IPaymentMethodAvailability, PolicyPaymentMethodAvailability>();
        services.TryAddScoped<IShipmentTracking, DeliveryShipmentTracking>();

        services.AddScoped<OrderQueryService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<SubstitutionPricer>();
        services.AddScoped<OrderCommandService>();
        services.AddScoped<ComplaintService>();
        services.AddScoped<OrderEventService>();
        services.AddEventHandler<PaymentCaptured, OrderingPaymentCapturedHandler>();
        services.AddEventHandler<PaymentFailed, OrderingPaymentFailedHandler>();
        services.AddEventHandler<ShipmentDispatched, OrderingShipmentDispatchedHandler>();
        services.AddEventHandler<ShipmentDelivered, OrderingShipmentDeliveredHandler>();
        services.AddEventHandler<DeliveryAttemptFailed, OrderingDeliveryAttemptFailedHandler>();
        services.AddEventHandler<ShipmentReattempted, OrderingShipmentReattemptedHandler>();
        services.AddEventHandler<ShipmentReturned, OrderingShipmentReturnedHandler>();
        services.AddScoped<OrderExpiryService>();

        services.AddScoped<IOrderingApi, OrderingApi>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICityRetirementCheck, OrderingCityRetirementCheck>());
        services.AddScoped<OrderingCustomerDeletionCheck>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICustomerDeletionCheck, OrderingCustomerDeletionCheck>());

        services.AddScoped<ExpireUnpaidOrdersJob>();
        services.AddScoped<ExpireSubstitutionsJob>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRecurringJobRegistration, OrderingJobs>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISamplePurgeParticipant, OrderingSamplePurgeParticipant>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => OrderingEndpoints.Map(endpoints);
}
