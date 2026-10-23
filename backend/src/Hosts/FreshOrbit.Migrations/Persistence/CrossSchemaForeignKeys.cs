namespace FreshOrbit.Migrations.Persistence;

/// <summary>
/// Foreign keys from one module's schema to another's (docs/database/README.md: allowed across schemas, never navigated
/// in code). EF does not model them because no module references another module's entities, so the initial migration
/// adds them as SQL. They point at <c>ordering.orders</c>, <c>locations.fulfillment_locations</c> and
/// <c>locations.cities</c>; they are <c>NO ACTION DEFERRABLE INITIALLY DEFERRED</c> (refuse deleting a referenced row, like
/// RESTRICT, but checked at commit) because one SaveChanges inserts rows of several modules in an order EF cannot know.
/// <c>payments.reconciliation_issues.order_id</c> is left out: an issue may name an order this system never stored.
/// </summary>
internal static class CrossSchemaForeignKeys
{
    private static readonly (string Table, string Column, string Target)[] Keys =
    [
        ("delivery.shipments", "order_id", "ordering.orders"),
        ("inventory.stock_reservations", "order_id", "ordering.orders"),
        ("payments.payments", "order_id", "ordering.orders"),
        ("payments.refunds", "order_id", "ordering.orders"),
        ("promotions.coupon_redemptions", "order_id", "ordering.orders"),

        ("cart.carts", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("delivery.delivery_slots", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("delivery.riders", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("delivery.shipments", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("delivery.slot_templates", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("identity.staff_location_access", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("inventory.batches", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("inventory.cold_chain_logs", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("inventory.low_stock_digests", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("inventory.stock_items", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("ordering.orders", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("ordering.complaints", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("payments.cash_settlements", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("payments.payments", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("payments.refunds", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("pricing.preparation_charges", "fulfillment_location_id", "locations.fulfillment_locations"),
        ("pricing.variant_prices", "fulfillment_location_id", "locations.fulfillment_locations"),

        ("ordering.orders", "city_id", "locations.cities"),
        ("promotions.coupons", "city_id", "locations.cities"),
        ("promotions.promotions", "city_id", "locations.cities"),
        ("settings.order_policies", "city_id", "locations.cities"),
    ];

    public static IEnumerable<string> AddSql => Keys.Select(k =>
        $"ALTER TABLE {k.Table} ADD CONSTRAINT {Name(k)} FOREIGN KEY ({k.Column}) REFERENCES {k.Target} (id) ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;");

    public static IEnumerable<string> DropSql => Keys.Select(k => $"ALTER TABLE {k.Table} DROP CONSTRAINT IF EXISTS {Name(k)};");

    private static string Name((string Table, string Column, string Target) key)
        => $"fk_{key.Table.Split('.')[1]}_{key.Column}";
}
