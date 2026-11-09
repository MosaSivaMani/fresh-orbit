using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshOrbit.Migrations.Persistence.Migrations
{
    /// <summary>
    /// Grievance officer in the business profile (D374), hand-written (no designer model; the snapshot holds the target
    /// model): name, designation, e-mail, phone and response time, all optional until the owner enters them.
    /// </summary>
    [DbContext(typeof(FreshOrbitDbContext))]
    [Migration("20261109090000_GrievanceOfficer")]
    public partial class GrievanceOfficer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grievance_officer_name",
                schema: "settings",
                table: "business_profile",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "grievance_officer_designation",
                schema: "settings",
                table: "business_profile",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "grievance_officer_email",
                schema: "settings",
                table: "business_profile",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "grievance_officer_phone",
                schema: "settings",
                table: "business_profile",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "grievance_response_time",
                schema: "settings",
                table: "business_profile",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grievance_officer_name",
                schema: "settings",
                table: "business_profile");

            migrationBuilder.DropColumn(
                name: "grievance_officer_designation",
                schema: "settings",
                table: "business_profile");

            migrationBuilder.DropColumn(
                name: "grievance_officer_email",
                schema: "settings",
                table: "business_profile");

            migrationBuilder.DropColumn(
                name: "grievance_officer_phone",
                schema: "settings",
                table: "business_profile");

            migrationBuilder.DropColumn(
                name: "grievance_response_time",
                schema: "settings",
                table: "business_profile");
        }
    }
}
