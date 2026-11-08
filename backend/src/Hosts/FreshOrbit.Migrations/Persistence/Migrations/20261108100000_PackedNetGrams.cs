using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshOrbit.Migrations.Persistence.Migrations
{
    /// <summary>
    /// Fixed net-weight packs (D348), hand-written (no designer model; the snapshot holds the target model): the net
    /// grams weighed at pack for a line sold per PACK. Recorded only; the charge of a pack never changes.
    /// </summary>
    [DbContext(typeof(FreshOrbitDbContext))]
    [Migration("20261108100000_PackedNetGrams")]
    public partial class PackedNetGrams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "packed_net_grams",
                schema: "ordering",
                table: "order_items",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "packed_net_grams",
                schema: "ordering",
                table: "order_items");
        }
    }
}
