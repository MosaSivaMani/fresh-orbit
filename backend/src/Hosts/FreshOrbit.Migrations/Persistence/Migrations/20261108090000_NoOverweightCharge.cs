using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshOrbit.Migrations.Persistence.Migrations
{
    /// <summary>
    /// Data patch for decision D347 ("the price you see is the most you pay"), hand-written (no designer model; the
    /// model does not change): order policies still at the old +10 % default (1000 bp) are set to 0, so packing more
    /// than ordered is never charged. A policy an admin set to another value is left as it is. Orders already placed
    /// keep the max chargeable quantity snapshotted at placement.
    /// </summary>
    [DbContext(typeof(FreshOrbitDbContext))]
    [Migration("20261108090000_NoOverweightCharge")]
    public partial class NoOverweightCharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE settings.order_policies SET max_overweight_bp = 0 WHERE max_overweight_bp = 1000;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible: a row at 0 cannot tell whether it was patched or set by an admin. Nothing to undo.
        }
    }
}
