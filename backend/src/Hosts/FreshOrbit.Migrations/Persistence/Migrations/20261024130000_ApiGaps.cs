using System;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshOrbit.Migrations.Persistence.Migrations
{
    /// <summary>
    /// Track E API gaps, hand-written (no designer model; the snapshot holds the target model): variant serving facts
    /// (CAT-R21), complaint photos (W12), stock alerts (CUS-R10) and business-profile support hours (W7). The
    /// <c>catalog.storefront_locations</c> view is recreated by the database objects step, not here.
    /// </summary>
    [DbContext(typeof(FreshOrbitDbContext))]
    [Migration("20261024130000_ApiGaps")]
    public partial class ApiGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "pieces_max",
                schema: "catalog",
                table: "product_variants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pieces_min",
                schema: "catalog",
                table: "product_variants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "serves",
                schema: "catalog",
                table: "product_variants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_variants_pieces",
                schema: "catalog",
                table: "product_variants",
                sql: "(pieces_min IS NULL AND pieces_max IS NULL) OR (pieces_min BETWEEN 1 AND 1000 AND pieces_max BETWEEN pieces_min AND 1000)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_variants_serves",
                schema: "catalog",
                table: "product_variants",
                sql: "serves IS NULL OR serves BETWEEN 1 AND 100");

            migrationBuilder.AddColumn<string>(
                name: "support_hours",
                schema: "settings",
                table: "business_profile",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "complaint_photos",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    complaint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_complaint_photos", x => x.id);
                    table.CheckConstraint("ck_complaint_photos_size", "width > 0 AND height > 0 AND size_bytes > 0");
                    table.ForeignKey(
                        name: "fk_complaint_photos_complaints_complaint_id",
                        column: x => x.complaint_id,
                        principalSchema: "ordering",
                        principalTable: "complaints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_complaint_photos_complaint_id_uploaded_at",
                schema: "ordering",
                table: "complaint_photos",
                columns: new[] { "complaint_id", "uploaded_at" });

            migrationBuilder.CreateTable(
                name: "stock_alerts",
                schema: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_alerts", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_alerts_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "customers",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_alerts_customer_id_variant_id_fulfillment_location_id",
                schema: "customers",
                table: "stock_alerts",
                columns: new[] { "customer_id", "variant_id", "fulfillment_location_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_alerts_variant_id_fulfillment_location_id",
                schema: "customers",
                table: "stock_alerts",
                columns: new[] { "variant_id", "fulfillment_location_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_alerts",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "complaint_photos",
                schema: "ordering");

            migrationBuilder.DropColumn(
                name: "support_hours",
                schema: "settings",
                table: "business_profile");

            migrationBuilder.DropCheckConstraint(
                name: "ck_product_variants_pieces",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_product_variants_serves",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "pieces_max",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "pieces_min",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "serves",
                schema: "catalog",
                table: "product_variants");
        }
    }
}
