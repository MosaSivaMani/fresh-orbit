using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshOrbit.Migrations.Persistence.Migrations
{
    /// <summary>
    /// Invoice PDF (L-12, D342), hand-written (no designer model; the snapshot holds the target model): the seller's
    /// registered address and the supplying premises' FSSAI licence frozen on the invoice when it is issued. Nullable:
    /// invoices issued before this change have neither.
    /// </summary>
    [DbContext(typeof(FreshOrbitDbContext))]
    [Migration("20261108120000_InvoiceSellerSnapshot")]
    public partial class InvoiceSellerSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "seller_address",
                schema: "ordering",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seller_fssai_license_no",
                schema: "ordering",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "seller_fssai_license_no",
                schema: "ordering",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "seller_address",
                schema: "ordering",
                table: "invoices");
        }
    }
}
