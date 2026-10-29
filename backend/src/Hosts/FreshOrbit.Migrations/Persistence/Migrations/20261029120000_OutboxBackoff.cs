using System;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshOrbit.Migrations.Persistence.Migrations
{
    /// <summary>
    /// BE-2 (D296), hand-written (no designer model; the snapshot holds the target model): the outbox backoff time
    /// <c>platform.outbox.next_attempt_at</c> (null = due now, so rows already waiting stay due) and the staff replay of
    /// dead letters (<c>replayed_at</c>, <c>replayed_by</c>, plus an index of the open ones).
    /// </summary>
    [DbContext(typeof(FreshOrbitDbContext))]
    [Migration("20261029120000_OutboxBackoff")]
    public partial class OutboxBackoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                schema: "platform",
                table: "outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "replayed_at",
                schema: "platform",
                table: "dead_letters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "replayed_by",
                schema: "platform",
                table: "dead_letters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_dead_letters_failed_at",
                schema: "platform",
                table: "dead_letters",
                column: "failed_at",
                filter: "replayed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_dead_letters_failed_at",
                schema: "platform",
                table: "dead_letters");

            migrationBuilder.DropColumn(
                name: "replayed_by",
                schema: "platform",
                table: "dead_letters");

            migrationBuilder.DropColumn(
                name: "replayed_at",
                schema: "platform",
                table: "dead_letters");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                schema: "platform",
                table: "outbox");
        }
    }
}
