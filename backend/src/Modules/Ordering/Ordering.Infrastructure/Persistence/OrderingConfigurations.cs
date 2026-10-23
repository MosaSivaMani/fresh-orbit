using FreshOrbit.Ordering.Domain.Complaints;
using FreshOrbit.Ordering.Domain.Invoices;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.SharedKernel.Codes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Ordering.Infrastructure.Persistence;

internal static class Checks
{
    public static string In<TEnum>(string column)
        where TEnum : struct, Enum
        => $"{column} IN ({string.Join(", ", EnumCodes.AllCodes<TEnum>().Select(c => $"'{c}'"))})";
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", t =>
        {
            t.HasCheckConstraint("ck_orders_status", Checks.In<OrderStatus>("status"));
            t.HasCheckConstraint("ck_orders_payment_method", Checks.In<PaymentMethod>("payment_method"));
            t.HasCheckConstraint("ck_orders_substitution_preference", Checks.In<SubstitutionPreference>("substitution_preference"));
            t.HasCheckConstraint("ck_orders_amounts", "total_paise >= 0 AND amount_due_paise >= 0 AND refund_due_paise >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.OrderNumber).HasMaxLength(30);
        builder.Property(x => x.TimeZone).HasMaxLength(64);
        builder.Property(x => x.RecipientName).HasMaxLength(120);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.Line1).HasMaxLength(200);
        builder.Property(x => x.Line2).HasMaxLength(200);
        builder.Property(x => x.Landmark).HasMaxLength(200);
        builder.Property(x => x.Locality).HasMaxLength(120);
        builder.Property(x => x.CityName).HasMaxLength(120);
        builder.Property(x => x.StateCode).HasMaxLength(10);
        builder.Property(x => x.Pincode).HasMaxLength(10);
        builder.Property(x => x.Latitude).HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasPrecision(9, 6);
        builder.Property(x => x.CustomerNotes).HasMaxLength(Order.MaxCustomerNotesLength);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100);
        builder.Property(x => x.CouponCode).HasMaxLength(20);
        builder.Property(x => x.CancelReasonCode).HasMaxLength(40);
        builder.HasIndex(x => x.OrderNumber).IsUnique();
        builder.HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.Status, x.SlotStartAt });
        builder.HasIndex(x => new { x.CustomerId, x.PlacedAt }).IsDescending(false, true);
        builder.HasIndex(x => x.DeliverySlotId);
        builder.HasIndex(x => x.PaymentDueAt).HasFilter("status = 'PENDING_PAYMENT'");
        builder.HasIndex(x => x.CityId);

        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.History).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.Adjustments).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Adjustments).HasField("_adjustments").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.Substitutions).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Substitutions).HasField("_substitutions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", t => t.HasCheckConstraint("ck_order_items_status", Checks.In<OrderItemStatus>("status")));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ProductName).HasMaxLength(200);
        builder.Property(x => x.VariantName).HasMaxLength(200);
        builder.Property(x => x.Sku).HasMaxLength(64);
        builder.Property(x => x.ImageKey).HasMaxLength(300);
        builder.Property(x => x.HsnCode).HasMaxLength(10);
        builder.Property(x => x.PreparationCode).HasMaxLength(40);
        builder.Property(x => x.PreparationName).HasMaxLength(120);
        builder.Property(x => x.SellUnit).HasMaxLength(10);
        builder.Property(x => x.StockUnit).HasMaxLength(10);
        builder.Property(x => x.QuantityUnit).HasMaxLength(10);
        builder.Property(x => x.ChargeBasis).HasMaxLength(10);
        builder.Ignore(x => x.Description);
        builder.HasIndex(x => new { x.OrderId, x.LineNo }).IsUnique();
        builder.HasIndex(x => x.VariantId);
    }
}

internal sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("order_status_history");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ReasonCode).HasMaxLength(40);
        builder.HasIndex(x => new { x.OrderId, x.At });
    }
}

internal sealed class OrderAdjustmentConfiguration : IEntityTypeConfiguration<OrderAdjustment>
{
    public void Configure(EntityTypeBuilder<OrderAdjustment> builder)
    {
        builder.ToTable("order_adjustments", t => t.HasCheckConstraint("ck_order_adjustments_kind", Checks.In<AdjustmentKind>("kind")));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Reason).HasMaxLength(200);
        builder.HasIndex(x => x.OrderId);
    }
}

internal sealed class SubstitutionConfiguration : IEntityTypeConfiguration<Substitution>
{
    public void Configure(EntityTypeBuilder<Substitution> builder)
    {
        builder.ToTable("substitutions", t => t.HasCheckConstraint("ck_substitutions_status", Checks.In<SubstitutionStatus>("status")));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ProposedName).HasMaxLength(400);
        builder.Property(x => x.ProposedPreparationCode).HasMaxLength(40);
        builder.Property(x => x.QuantityUnit).HasMaxLength(10);
        builder.Property(x => x.StockUnit).HasMaxLength(10);
        builder.HasIndex(x => x.OrderItemId).IsUnique().HasFilter("status = 'PROPOSED'");
        builder.HasIndex(x => x.ExpiresAt).HasFilter("status = 'PROPOSED'");
    }
}

internal sealed class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> builder)
    {
        builder.ToTable("complaints", t =>
        {
            t.HasCheckConstraint("ck_complaints_kind", Checks.In<ComplaintKind>("kind"));
            t.HasCheckConstraint("ck_complaints_status", Checks.In<ComplaintStatus>("status"));
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.OrderNumber).HasMaxLength(30);
        builder.Property(x => x.Description).HasMaxLength(Complaint.MaxDescriptionLength);
        builder.Property(x => x.DecisionNote).HasMaxLength(500);
        builder.HasIndex(x => new { x.Status, x.RaisedAt });
        builder.HasIndex(x => x.OrderId);

        // ORD-R55: one OPEN/APPROVED complaint per line, per order for LATE (null line).
        builder.HasIndex(x => new { x.OrderId, x.OrderItemId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("status IN ('OPEN', 'APPROVED')")
            .HasDatabaseName("ux_complaints_open_per_line");
    }
}

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.InvoiceNumber).HasMaxLength(16);
        builder.Property(x => x.SellerGstin).HasMaxLength(15);
        builder.Property(x => x.SellerLegalName).HasMaxLength(200);
        builder.Property(x => x.PdfStorageKey).HasMaxLength(300);
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("invoice_lines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Description).HasMaxLength(400);
        builder.Property(x => x.HsnCode).HasMaxLength(10);
        builder.Property(x => x.Unit).HasMaxLength(10);
        builder.HasIndex(x => new { x.InvoiceId, x.LineNo }).IsUnique();
    }
}
