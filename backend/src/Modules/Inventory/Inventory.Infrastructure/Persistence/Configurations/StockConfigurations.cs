using FreshOrbit.Inventory.Domain.ColdChain;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.SharedKernel.Codes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Inventory.Infrastructure.Persistence.Configurations;

internal static class Checks
{
    public static string In<TEnum>(string column)
        where TEnum : struct, Enum
        => $"{column} IN ({string.Join(", ", EnumCodes.AllCodes<TEnum>().Select(c => $"'{c}'"))})";
}

internal sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items", t =>
        {
            t.HasCheckConstraint("ck_stock_items_on_hand", "on_hand >= 0");
            t.HasCheckConstraint("ck_stock_items_reserved", "reserved >= 0");
            t.HasCheckConstraint("ck_stock_items_threshold", "low_stock_threshold >= 0");
            t.HasCheckConstraint("ck_stock_items_stock_unit", Checks.In<StockUnit>("stock_unit"));
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.Available);
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.VariantId }).IsUnique().HasDatabaseName(InventoryConstraints.StockItemLocationVariant);
        builder.HasMany(x => x.Batches).WithOne().HasForeignKey(x => x.StockItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Movements).WithOne().HasForeignKey(x => x.StockItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Reservations).WithOne().HasForeignKey(x => x.StockItemId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Batches).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Movements).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Reservations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.ToTable("batches", t =>
        {
            t.HasCheckConstraint("ck_batches_received_quantity", "received_quantity > 0");
            t.HasCheckConstraint("ck_batches_remaining_quantity", "remaining_quantity >= 0");
            t.HasCheckConstraint("ck_batches_total_cost", "total_cost_paise >= 0");
            t.HasCheckConstraint("ck_batches_status", Checks.In<BatchStatus>("status"));
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.IsActive);
        builder.Property(x => x.BatchCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SupplierName).HasMaxLength(120);
        builder.Property(x => x.SourceLandingCenter).HasMaxLength(120);
        builder.Property(x => x.ReceiptTemperatureC).HasColumnType("numeric(4,1)");
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.BatchCode }).IsUnique().HasDatabaseName(InventoryConstraints.BatchLocationCode);
        builder.HasIndex(x => new { x.VariantId, x.BestBeforeAt }).HasFilter("status = 'ACTIVE'").HasDatabaseName("ix_batches_variant_best_before_active");
        builder.HasIndex(x => x.BestBeforeAt).HasFilter("status = 'ACTIVE'").HasDatabaseName("ix_batches_best_before_active");
        builder.HasIndex(x => x.StockItemId);
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements", t =>
        {
            t.HasCheckConstraint("ck_stock_movements_kind", Checks.In<MovementKind>("kind"));
            t.HasCheckConstraint("ck_stock_movements_reference_type", Checks.In<MovementReferenceType>("reference_type"));
            t.HasCheckConstraint("ck_stock_movements_quantity", "quantity <> 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ReasonCode).HasMaxLength(40);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasOne<Batch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.StockItemId, x.OccurredAt });
        builder.HasIndex(x => x.BatchId);
        builder.HasIndex(x => x.ReferenceId).HasFilter("kind = 'SALE'").HasDatabaseName("ix_stock_movements_sale_reference");
    }
}

internal sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("stock_reservations", t =>
        {
            t.HasCheckConstraint("ck_stock_reservations_quantity", "quantity > 0");
            t.HasCheckConstraint("ck_stock_reservations_status", Checks.In<ReservationStatus>("status"));
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.IsActive);
        builder.HasIndex(x => x.OrderItemId).IsUnique().HasFilter("status = 'ACTIVE'").HasDatabaseName(InventoryConstraints.ActiveReservationPerItem);
        builder.HasIndex(x => x.ExpiresAt).HasFilter("status = 'ACTIVE'").HasDatabaseName("ix_stock_reservations_expires_active");
        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.StockItemId);
    }
}

internal sealed class LowStockDigestConfiguration : IEntityTypeConfiguration<LowStockDigest>
{
    public void Configure(EntityTypeBuilder<LowStockDigest> builder)
    {
        builder.ToTable("low_stock_digests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.LocalDate }).IsUnique().HasDatabaseName(InventoryConstraints.DigestPerDay);
    }
}

internal sealed class StorageUnitRangeConfiguration : IEntityTypeConfiguration<StorageUnitRange>
{
    public void Configure(EntityTypeBuilder<StorageUnitRange> builder)
    {
        builder.ToTable("storage_units");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(40);
        builder.Property(x => x.MinC).HasColumnType("numeric(4,1)");
        builder.Property(x => x.MaxC).HasColumnType("numeric(4,1)");
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
    }
}

internal sealed class ColdChainLogConfiguration : IEntityTypeConfiguration<ColdChainLog>
{
    public void Configure(EntityTypeBuilder<ColdChainLog> builder)
    {
        builder.ToTable("cold_chain_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.StorageUnit).HasMaxLength(40).IsRequired();
        builder.Property(x => x.TemperatureC).HasColumnType("numeric(4,1)");
        builder.HasOne<StorageUnitRange>().WithMany().HasForeignKey(x => x.StorageUnit).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.RecordedAt }).IsDescending(false, true);
    }
}

/// <summary>Names of the constraints the unit of work translates into refusals.</summary>
internal static class InventoryConstraints
{
    public const string StockItemLocationVariant = "ux_stock_items_location_variant";
    public const string BatchLocationCode = "ux_batches_location_code";
    public const string ActiveReservationPerItem = "ux_stock_reservations_active_order_item";
    public const string DigestPerDay = "ux_low_stock_digests_location_date";
}
