using FreshOrbit.Customers.Public;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.Payments.Infrastructure.Seeding;
using FreshOrbit.Delivery.Public.Events;
using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Handlers;
using FreshOrbit.Payments.Application.Payments;
using FreshOrbit.Payments.Application.Providers;
using FreshOrbit.Payments.Application.Reconciliation;
using FreshOrbit.Payments.Application.Refunds;
using FreshOrbit.Payments.Application.Settlements;
using FreshOrbit.Payments.Application.Webhooks;
using FreshOrbit.Payments.Public.Events;
using FreshOrbit.Payments.Infrastructure.Endpoints;
using FreshOrbit.Payments.Infrastructure.Jobs;
using FreshOrbit.Payments.Infrastructure.Persistence;
using FreshOrbit.Payments.Infrastructure.Persistence.Repositories;
using FreshOrbit.Payments.Infrastructure.Providers;
using FreshOrbit.Payments.Infrastructure.Public;
using FreshOrbit.Payments.Infrastructure.Reads;
using FreshOrbit.Payments.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Payments.Infrastructure;

/// <summary>Composition root of the Payments module.</summary>
public sealed class PaymentsModule : IModule
{
    public string Name => "Payments";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<PaymentsModuleModel>();

        // Options are bound when first read, so hosts and tests can override configuration.
        services.AddOptions<PaymentsOptions>().BindConfiguration(PaymentsOptions.SectionName);
        services.AddOptions<PaymentProvidersOptions>()
            .Configure<IConfiguration>((options, config) => config.GetSection(PaymentProvidersOptions.SectionName).Bind(options.Adapters))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PaymentProvidersOptions>, PaymentProvidersOptionsValidator>());
        services.TryAddTransient(provider => provider.GetRequiredService<IOptionsMonitor<PaymentsOptions>>().CurrentValue);

        // Provider adapters (PAY-R1, R2).
        services.TryAddSingleton<FakeGateway>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPaymentProvider, CashOnDeliveryProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPaymentProvider, UpiOnDeliveryProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPaymentProvider, FakePaymentProvider>());
        services.TryAddSingleton<IPaymentProviderCatalog, PaymentProviderCatalog>();

        // Persistence.
        services.TryAddScoped<IPaymentsSession, PaymentsSession>();
        services.TryAddScoped<IPaymentRepository, PaymentRepository>();
        services.TryAddScoped<IWebhookEventRepository, WebhookEventRepository>();
        services.TryAddScoped<ICashSettlementRepository, CashSettlementRepository>();
        services.TryAddScoped<IReconciliationIssueRepository, ReconciliationIssueRepository>();
        services.TryAddScoped<PaymentsReads>();

        // Ports to other modules.
        services.TryAddScoped<IShipmentFacts, DeliveryShipmentFacts>();

        // Application services.
        services.TryAddScoped<IssueRecorder>();
        services.TryAddScoped<OnlinePaymentService>();
        services.TryAddScoped<PaymentEventApplier>();
        services.TryAddScoped<WebhookService>();
        services.TryAddScoped<RefundService>();
        services.TryAddScoped<RefundProcessor>();
        services.TryAddScoped<CashCollectionService>();
        services.TryAddScoped<CashSettlementService>();
        services.TryAddScoped<PaymentIssueService>();
        services.TryAddScoped<ReconciliationService>();
        services.TryAddScoped<OrderPaymentHandlers>();
        services.AddEventHandler<OrderPlaced, PaymentsOrderPlacedHandler>();
        services.AddEventHandler<OrderPacked, PaymentsOrderPackedHandler>();
        services.AddEventHandler<OrderAdjusted, PaymentsOrderAdjustedHandler>();
        services.AddEventHandler<OrderCancelled, PaymentsOrderCancelledHandler>();
        services.AddEventHandler<OrderPaymentFailed, PaymentsOrderPaymentFailedHandler>();
        services.AddEventHandler<OrderReturned, PaymentsOrderReturnedHandler>();
        services.AddEventHandler<ShipmentDelivered, PaymentsShipmentDeliveredHandler>();
        services.AddEventHandler<PaymentReceivedForClosedOrder, PaymentsReceivedForClosedOrderHandler>();
        services.AddEventHandler<ComplaintDecided, PaymentsComplaintDecidedHandler>();

        // Public API, own event handlers and jobs.
        services.TryAddScoped<IPaymentsApi, PaymentsApi>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICustomerDeletionCheck, PaymentsCustomerDeletionCheck>());
        services.AddEventHandler<RefundApproved, RefundApprovedHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRecurringJobRegistration, PaymentsJobRegistration>());
        services.TryAddScoped<ReconciliationJob>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISamplePurgeParticipant, PaymentsSamplePurgeParticipant>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        PaymentsEndpoints.MapCustomer(endpoints);
        PaymentsEndpoints.MapAdmin(endpoints);
        var environment = endpoints.ServiceProvider.GetService<IHostEnvironment>();
        if (environment is not null && (environment.IsDevelopment() || environment.IsEnvironment("Testing")))
        {
            PaymentsEndpoints.MapDevelopment(endpoints);
        }
    }
}
