using AwesomeAssertions;
using FreshOrbit.SharedKernel.Events;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.ArchitectureTests;

/// <summary>
/// What the hosts compose from <c>Modules.All</c>: every Public interface has an implementation registered, and every
/// cross-module event adapter of docs/team/integration.md is registered in its consumer module.
/// </summary>
public sealed class CompositionTests
{
    private static readonly IServiceCollection Services = BuildServices();

    public static TheoryData<string> PublicInterfaces => [.. ModuleCatalog.Modules
        .SelectMany(m => ModuleCatalog.Load(m, "Public").GetTypes())
        .Where(t => t.IsInterface && t.IsPublic && !t.IsGenericType)
        .Select(t => t.AssemblyQualifiedName!)];

    /// <summary>Event (owner module, type name) → consumer module.</summary>
    public static TheoryData<string, string, string> Adapters => new()
    {
        { "Ordering", "OrderConfirmed", "Inventory" },
        { "Ordering", "OrderPaymentFailed", "Inventory" },
        { "Ordering", "OrderCancelled", "Inventory" },
        { "Ordering", "OrderPacked", "Inventory" },
        { "Ordering", "OrderPlaced", "Delivery" },
        { "Ordering", "OrderCancelled", "Delivery" },
        { "Ordering", "OrderPaymentFailed", "Delivery" },
        { "Ordering", "OrderPacked", "Delivery" },
        { "Locations", "LocationClosureAdded", "Delivery" },
        { "Locations", "LocationClosureRemoved", "Delivery" },
        { "Locations", "FulfillmentLocationStatusChanged", "Delivery" },
        { "Ordering", "OrderPlaced", "Payments" },
        { "Ordering", "OrderPacked", "Payments" },
        { "Ordering", "OrderAdjusted", "Payments" },
        { "Ordering", "OrderCancelled", "Payments" },
        { "Ordering", "OrderPaymentFailed", "Payments" },
        { "Ordering", "OrderReturned", "Payments" },
        { "Delivery", "ShipmentDelivered", "Payments" },
        { "Ordering", "PaymentReceivedForClosedOrder", "Payments" },
        { "Ordering", "ComplaintDecided", "Payments" },
        { "Ordering", "OrderCancelled", "Promotions" },
        { "Ordering", "OrderPaymentFailed", "Promotions" },
        { "Payments", "PaymentCaptured", "Ordering" },
        { "Payments", "PaymentFailed", "Ordering" },
        { "Delivery", "ShipmentDispatched", "Ordering" },
        { "Delivery", "ShipmentDelivered", "Ordering" },
        { "Delivery", "DeliveryAttemptFailed", "Ordering" },
        { "Delivery", "ShipmentReattempted", "Ordering" },
        { "Delivery", "ShipmentReturned", "Ordering" },
        { "Identity", "CustomerSignedIn", "Cart" },
        { "Ordering", "OrderPlaced", "Notifications" },
        { "Ordering", "OrderConfirmed", "Notifications" },
        { "Ordering", "OrderPacked", "Notifications" },
        { "Ordering", "OrderDispatched", "Notifications" },
        { "Ordering", "OrderDelivered", "Notifications" },
        { "Ordering", "OrderCancelled", "Notifications" },
        { "Ordering", "OrderPaymentFailed", "Notifications" },
        { "Ordering", "OrderStatusChanged", "Notifications" },
        { "Payments", "PaymentCaptured", "Notifications" },
        { "Payments", "PaymentFailed", "Notifications" },
        { "Payments", "RefundSucceeded", "Notifications" },
        { "Customers", "CustomerDeleted", "Notifications" },
    };

    [Theory]
    [MemberData(nameof(PublicInterfaces))]
    public void Every_public_interface_is_registered(string interfaceName)
    {
        var type = Type.GetType(interfaceName, throwOnError: true)!;

        Services.Should().Contain(d => d.ServiceType == type, $"{type.FullName} must have an implementation registered by a module");
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public void Every_cross_module_event_has_its_adapter(string owner, string eventName, string consumer)
    {
        var eventType = ModuleCatalog.Load(owner, "Public").GetTypes().Single(t => t.Name == eventName && typeof(DomainEvent).IsAssignableFrom(t));
        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);

        Services.Where(d => d.ServiceType == handlerType)
            .Select(d => d.ImplementationType?.Namespace ?? string.Empty)
            .Should().Contain(ns => ns.StartsWith($"FreshOrbit.{consumer}.Application", StringComparison.Ordinal), $"{consumer} must handle {eventName}");
    }

    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddFreshOrbitModules(new ConfigurationBuilder().Build(), Hosting.Modules.All);
        return services;
    }
}
