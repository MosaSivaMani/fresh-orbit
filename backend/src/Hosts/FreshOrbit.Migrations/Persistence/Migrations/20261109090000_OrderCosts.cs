using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshOrbit.Migrations.Persistence.Migrations
{
    /// <summary>
    /// Delivery and packaging cost per order (D365–D367), hand-written (no designer model; the snapshot holds the target
    /// model): the per-location cost rates staff enter, the costs frozen on each shipment at delivery (null on shipments
    /// delivered before this change: "not configured"), and the <c>reporting.contribution_margin</c> view dropped so the
    /// migration runner recreates it from <c>03_contribution_margin.sql</c> with the new columns (it is created with
    /// <c>IF NOT EXISTS</c>, so a changed definition only lands after a drop).
    /// </summary>
    [DbContext(typeof(FreshOrbitDbContext))]
    [Migration("20261109090000_OrderCosts")]
    public partial class OrderCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "location_cost_rates",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_cost_per_drop_paise = table.Column<long>(type: "bigint", nullable: true),
                    packaging_cost_per_order_paise = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_cost_rates", x => x.id);
                    table.CheckConstraint("ck_location_cost_rates_delivery", "delivery_cost_per_drop_paise IS NULL OR delivery_cost_per_drop_paise BETWEEN 0 AND 100000");
                    table.CheckConstraint("ck_location_cost_rates_packaging", "packaging_cost_per_order_paise IS NULL OR packaging_cost_per_order_paise BETWEEN 0 AND 50000");
                });

            migrationBuilder.CreateIndex(
                name: "ix_location_cost_rates_fulfillment_location_id",
                schema: "delivery",
                table: "location_cost_rates",
                column: "fulfillment_location_id",
                unique: true);

            migrationBuilder.AddColumn<long>(
                name: "delivery_cost_paise",
                schema: "delivery",
                table: "shipments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "packaging_cost_paise",
                schema: "delivery",
                table: "shipments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_shipments_delivery_cost",
                schema: "delivery",
                table: "shipments",
                sql: "delivery_cost_paise IS NULL OR delivery_cost_paise >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shipments_packaging_cost",
                schema: "delivery",
                table: "shipments",
                sql: "packaging_cost_paise IS NULL OR packaging_cost_paise >= 0");

            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS reporting.contribution_margin;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The view reads the shipment cost columns; the runner recreates it from the SQL file of the code it runs.
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS reporting.contribution_margin;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shipments_packaging_cost",
                schema: "delivery",
                table: "shipments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shipments_delivery_cost",
                schema: "delivery",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "packaging_cost_paise",
                schema: "delivery",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "delivery_cost_paise",
                schema: "delivery",
                table: "shipments");

            migrationBuilder.DropTable(
                name: "location_cost_rates",
                schema: "delivery");
        }
    }
}
