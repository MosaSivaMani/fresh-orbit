using FreshOrbit.Delivery.Domain.Riders;
using FreshOrbit.Delivery.Domain.Shipments;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Domain.Templates;
using FreshOrbit.SharedKernel.Codes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Delivery.Infrastructure.Persistence.Configurations;

internal static class Checks
{
    public static string In<TEnum>(string column)
        where TEnum : struct, Enum
        => $"{column} IN ({string.Join(", ", EnumCodes.AllCodes<TEnum>().Select(c => $"'{c}'"))})";
}

internal sealed class SlotTemplateConfiguration : IEntityTypeConfiguration<SlotTemplate>
{
    public void Configure(EntityTypeBuilder<SlotTemplate> builder)
    {
        builder.ToTable("slot_templates", t =>
        {
            t.HasCheckConstraint("ck_slot_templates_day_of_week", "day_of_week BETWEEN 1 AND 7");
            t.HasCheckConstraint("ck_slot_templates_times", "start_time < end_time");
            t.HasCheckConstraint("ck_slot_templates_capacity", $"capacity BETWEEN {SlotTemplate.MinCapacity} AND {SlotTemplate.MaxCapacity}");
            t.HasCheckConstraint("ck_slot_templates_cutoff", $"cutoff_minutes BETWEEN 0 AND {SlotTemplate.MaxCutoffMinutes}");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.StartTime).HasColumnType("time without time zone");
        builder.Property(x => x.EndTime).HasColumnType("time without time zone");
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.DayOfWeek });
    }
}

internal sealed class DeliverySlotConfiguration : IEntityTypeConfiguration<DeliverySlot>
{
    public void Configure(EntityTypeBuilder<DeliverySlot> builder)
    {
        builder.ToTable("delivery_slots", t =>
        {
            t.HasCheckConstraint("ck_delivery_slots_booked_count", "booked_count >= 0 AND booked_count <= capacity");
            t.HasCheckConstraint("ck_delivery_slots_capacity", $"capacity BETWEEN 0 AND {DeliverySlot.MaxCapacity}");
            t.HasCheckConstraint("ck_delivery_slots_times", "start_at < end_at AND cutoff_at <= start_at");
            t.HasCheckConstraint("ck_delivery_slots_status", Checks.In<SlotStatus>("status"));
            t.HasCheckConstraint("ck_delivery_slots_closed_reason", $"closed_reason IS NULL OR {Checks.In<SlotClosedReason>("closed_reason")}");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.Remaining);
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.StartAt }).IsUnique();
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.SlotDate });
        builder.HasIndex(x => x.SlotTemplateId);
        builder.HasOne<SlotTemplate>().WithMany().HasForeignKey(x => x.SlotTemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RiderConfiguration : IEntityTypeConfiguration<Rider>
{
    public void Configure(EntityTypeBuilder<Rider> builder)
    {
        builder.ToTable("riders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FullName).HasMaxLength(Rider.MaxFullNameLength).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(Rider.MaxPhoneLength).IsRequired();
        builder.HasIndex(x => x.Phone).IsUnique().HasFilter("is_active");
        builder.HasIndex(x => x.FulfillmentLocationId);
    }
}

internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("shipments", t =>
        {
            t.HasCheckConstraint("ck_shipments_status", Checks.In<ShipmentStatus>("status"));
            t.HasCheckConstraint("ck_shipments_cash_mode", $"cash_mode IS NULL OR {Checks.In<CashMode>("cash_mode")}");
            t.HasCheckConstraint("ck_shipments_code_failures", "delivery_code_failures >= 0");
            t.HasCheckConstraint("ck_shipments_cash", "cash_collected_paise IS NULL OR cash_collected_paise >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.DispatchTemperatureC).HasPrecision(4, 1);
        builder.Property(x => x.DeliveryCode).HasColumnType("character(4)");
        builder.Property(x => x.UpiReference).HasMaxLength(Shipment.MaxUpiReferenceLength);
        builder.Ignore(x => x.IsDeliveryCodeLocked);
        builder.Ignore(x => x.NextAttemptNo);
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => new { x.RiderId, x.Status });
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.Status });
        builder.HasIndex(x => x.DeliverySlotId);
        builder.HasOne<DeliverySlot>().WithMany().HasForeignKey(x => x.DeliverySlotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Rider>().WithMany().HasForeignKey(x => x.RiderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Attempts).WithOne().HasForeignKey(a => a.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Attempts).HasField("_attempts").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> builder)
    {
        builder.ToTable("delivery_attempts", t => t.HasCheckConstraint("ck_delivery_attempts_outcome", Checks.In<AttemptOutcome>("outcome")));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Notes).HasMaxLength(Shipment.MaxNotesLength);
        builder.HasIndex(x => new { x.ShipmentId, x.AttemptNo }).IsUnique();
    }
}
