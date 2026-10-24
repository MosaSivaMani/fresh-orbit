using FreshOrbit.Ordering.Public.Events;
using FluentValidation;
using FreshOrbit.Inventory.Application.ColdChain;
using FreshOrbit.Inventory.Application.Common;
using FreshOrbit.Inventory.Application.Handlers;
using FreshOrbit.Inventory.Application.Jobs;
using FreshOrbit.Inventory.Application.Stock;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Inventory.Infrastructure.Endpoints;
using FreshOrbit.Inventory.Infrastructure.Jobs;
using FreshOrbit.Inventory.Infrastructure.Persistence;
using FreshOrbit.Inventory.Infrastructure.Persistence.Repositories;
using FreshOrbit.Inventory.Infrastructure.Public;
using FreshOrbit.Inventory.Infrastructure.Reads;
using FreshOrbit.Inventory.Infrastructure.Seeding;
using FreshOrbit.Inventory.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Inventory.Infrastructure;

/// <summary>Composition root of the Inventory module.</summary>
public sealed class InventoryModule : IModule
{
    public string Name => "Inventory";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<InventoryModuleModel>();

        services.AddScoped<IStockItemRepository, StockItemRepository>();
        services.AddScoped<IColdChainRepository, ColdChainRepository>();
        services.AddScoped<IInventoryJobQueries, InventoryJobQueries>();
        services.AddScoped<IInventoryUnitOfWork, InventoryUnitOfWork>();

        services.AddScoped<StockCommandService>();
        services.AddScoped<StockReservationService>();
        services.AddScoped<OrderStockHandlers>();
        services.AddEventHandler<OrderConfirmed, InventoryOrderConfirmedHandler>();
        services.AddEventHandler<OrderPaymentFailed, InventoryOrderPaymentFailedHandler>();
        services.AddEventHandler<OrderCancelled, InventoryOrderCancelledHandler>();
        services.AddEventHandler<OrderPacked, InventoryOrderPackedHandler>();
        services.AddScoped<BatchExpiryService>();
        services.AddScoped<LowStockDigestService>();
        services.AddScoped<InventoryReads>();

        services.AddScoped<IInventoryApi, InventoryApi>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICityActivationCheck, InventoryCityActivationCheck>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<Catalog.Public.IVariantFreshnessSource, InventoryFreshnessSource>());

        services.AddScoped<IValidator<CreateStockItemRequest>, CreateStockItemRequestValidator>();
        services.AddScoped<IValidator<SetThresholdRequest>, SetThresholdRequestValidator>();
        services.AddScoped<IValidator<ReceiveStockRequest>, ReceiveStockRequestValidator>();
        services.AddScoped<IValidator<DiscardBatchRequest>, DiscardBatchRequestValidator>();
        services.AddScoped<IValidator<RecordWasteRequest>, RecordWasteRequestValidator>();
        services.AddScoped<IValidator<AdjustStockRequest>, AdjustStockRequestValidator>();
        services.AddScoped<IValidator<CountStockRequest>, CountStockRequestValidator>();
        services.AddScoped<IValidator<RecordTemperatureRequest>, RecordTemperatureRequestValidator>();

        services.AddScoped<BatchExpiryJob>();
        services.AddScoped<LowStockDigestJob>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRecurringJobRegistration, InventoryJobRegistration>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IReferenceDataLoader, StorageUnitsReferenceDataLoader>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISampleDataLoader, InventorySampleDataLoader>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => InventoryEndpoints.Map(endpoints);
}
