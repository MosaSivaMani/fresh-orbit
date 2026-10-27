using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshOrbit.Migrations.Persistence.Migrations
{
    /// <summary>
    /// Client API gaps, hand-written (no designer model; the snapshot holds the target model): the optional state
    /// display name of a city (shown by <c>GET /serviceability</c>) and the optional badge of a home banner.
    /// </summary>
    [DbContext(typeof(FreshOrbitDbContext))]
    [Migration("20261027090000_ClientGaps")]
    public partial class ClientGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "state_name",
                schema: "locations",
                table: "cities",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "badge",
                schema: "promotions",
                table: "promotions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "badge",
                schema: "promotions",
                table: "promotions");

            migrationBuilder.DropColumn(
                name: "state_name",
                schema: "locations",
                table: "cities");
        }
    }
}
