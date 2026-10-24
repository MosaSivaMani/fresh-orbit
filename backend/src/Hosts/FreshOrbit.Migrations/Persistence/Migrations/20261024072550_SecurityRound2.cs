using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshOrbit.Migrations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecurityRound2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "payload_sha256",
                schema: "payments",
                table: "webhook_events",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "payload_truncated",
                schema: "payments",
                table: "webhook_events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // SEC-6: rows stored before this migration keep their full payload; hash it as stored.
            migrationBuilder.Sql(
                "UPDATE payments.webhook_events SET payload_sha256 = encode(sha256(convert_to(payload::text, 'UTF8')), 'hex') WHERE payload_sha256 = '';");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lockout_count",
                schema: "identity",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "staff_login_failures",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    client_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_login_failures", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_staff_login_failures_email_hash_client_ip_failed_at",
                schema: "identity",
                table: "staff_login_failures",
                columns: new[] { "email_hash", "client_ip", "failed_at" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_login_failures",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "payload_sha256",
                schema: "payments",
                table: "webhook_events");

            migrationBuilder.DropColumn(
                name: "payload_truncated",
                schema: "payments",
                table: "webhook_events");

            migrationBuilder.DropColumn(
                name: "locked_until",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "lockout_count",
                schema: "identity",
                table: "users");
        }
    }
}
