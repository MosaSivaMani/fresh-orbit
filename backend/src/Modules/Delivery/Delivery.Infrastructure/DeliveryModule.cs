using FreshOrbit.Locations.Public.Events;
using FreshOrbit.Ordering.Public.Events;
using FluentValidation;
using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Delivery.Application.Handlers;
using FreshOrbit.Delivery.Application.Reads;
using FreshOrbit.Delivery.Application.Riders;
using FreshOrbit.Delivery.Application.Shipments;
using FreshOrbit.Delivery.Application.Slots;
using FreshOrbit.Delivery.Application.Templates;
using FreshOrbit.Delivery.Domain.Riders;
using FreshOrbit.Delivery.Domain.Shipments;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Domain.Templates;
using FreshOrbit.Delivery.Domain.Templates.Events;
using FreshOrbit.Delivery.Infrastructure.Endpoints;
using FreshOrbit.Delivery.Infrastructure.Jobs;
using FreshOrbit.Delivery.Infrastructure.Locations;
using FreshOrbit.Delivery.Infrastructure.Persistence;
using FreshOrbit.Delivery.Infrastructure.Persistence.Repositories;
using FreshOrbit.Delivery.Infrastructure.Public;
using FreshOrbit.Delivery.Infrastructure.Reads;
using FreshOrbit.Delivery.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Delivery.Infrastructure;

/// <summary>Composition root of the Delivery module.</summary>
public sealed class DeliveryModule : IModule
{
    public string Name => "Delivery";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<DeliveryModuleModel>();

        services.AddScoped<ISlotTemplateRepository, SlotTemplateRepository>();
        services.AddScoped<IDeliverySlotRepository, DeliverySlotRepository>();
        services.AddScoped<IRiderRepository, RiderRepository>();
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IDeliveryReads, DeliveryReads>();

        services.TryAddScoped<ILocationCalendar, LocationsPublicCalendar>();
        services.TryAddScoped<IDeliveryPolicyProvider, OrderPolicyDeliveryPolicyProvider>();

        services.AddScoped<SlotTemplateService>();
        services.AddScoped<SlotService>();
        services.AddScoped<RiderService>();
        services.AddScoped<ShipmentService>();
        services.AddScoped<SlotGenerationService>();
        services.AddScoped<OrderEventHandlers>();
        services.AddScoped<LocationEventHandlers>();
        services.AddValidatorsFromAssemblyContaining<CreateSlotTemplateRequestValidator>(ServiceLifetime.Scoped);

        services.AddEventHandler<SlotTemplateChanged, SlotTemplateChangedHandler>();
        services.AddEventHandler<OrderPlaced, DeliveryOrderPlacedHandler>();
        services.AddEventHandler<OrderCancelled, DeliveryOrderCancelledHandler>();
        services.AddEventHandler<OrderPaymentFailed, DeliveryOrderPaymentFailedHandler>();
        services.AddEventHandler<OrderPacked, DeliveryOrderPackedHandler>();
        services.AddEventHandler<LocationClosureAdded, DeliveryClosureAddedHandler>();
        services.AddEventHandler<LocationClosureRemoved, DeliveryClosureRemovedHandler>();
        services.AddEventHandler<FulfillmentLocationStatusChanged, DeliveryLocationStatusChangedHandler>();

        services.AddScoped<IDeliveryApi, DeliveryApi>();
        services.AddScoped<INextSlotProvider, DeliveryNextSlotProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICityActivationCheck, DeliveryCityActivationCheck>());

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRecurringJobRegistration, DeliveryJobs>());
        services.AddScoped<SlotGenerationJob>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => DeliveryEndpoints.Map(endpoints);
}
