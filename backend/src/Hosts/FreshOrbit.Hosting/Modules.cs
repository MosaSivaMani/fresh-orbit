using FreshOrbit.Audit.Infrastructure;
using FreshOrbit.Cart.Infrastructure;
using FreshOrbit.Catalog.Infrastructure;
using FreshOrbit.Customers.Infrastructure;
using FreshOrbit.Delivery.Infrastructure;
using FreshOrbit.Identity.Infrastructure;
using FreshOrbit.Inventory.Infrastructure;
using FreshOrbit.Locations.Infrastructure;
using FreshOrbit.Notifications.Infrastructure;
using FreshOrbit.Ordering.Infrastructure;
using FreshOrbit.Payments.Infrastructure;
using FreshOrbit.Pricing.Infrastructure;
using FreshOrbit.Promotions.Infrastructure;
using FreshOrbit.Reporting.Infrastructure;
using FreshOrbit.Settings.Infrastructure;
using FreshOrbit.SharedKernel.Web.Modules;

namespace FreshOrbit.Hosting;

/// <summary>The explicit list of modules every host (Api, Worker, Migrations) and the test fixtures compose.</summary>
public static class Modules
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new IdentityModule(),
        new CustomersModule(),
        new LocationsModule(),
        new CatalogModule(),
        new PricingModule(),
        new InventoryModule(),
        new CartModule(),
        new PromotionsModule(),
        new OrderingModule(),
        new PaymentsModule(),
        new DeliveryModule(),
        new NotificationsModule(),
        new ReportingModule(),
        new AuditModule(),
        new SettingsModule(),
    ];
}
