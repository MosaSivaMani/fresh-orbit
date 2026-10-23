using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.Payments.Domain.Settlements;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.Payments.Domain.Webhooks;
using FreshOrbit.SharedKernel.Codes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Payments.Infrastructure.Persistence.Configurations;

internal static class CodeChecks
{
    public static string In<TEnum>(string column)
        where TEnum : struct, Enum
        => In(column, EnumCodes.AllCodes<TEnum>());

    public static string In(string column, IEnumerable<string> codes)
        => $"{column} IN ({string.Join(", ", codes.Select(c => $"'{c}'"))})";
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public static readonly string[] Providers = [PaymentProviders.Cash, PaymentProviders.UpiOnDelivery, PaymentProviders.Fake, PaymentProviders.Razorpay];

    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", t =>
        {
            t.HasCheckConstraint("ck_payments_method", CodeChecks.In<PaymentMethod>("method"));
            t.HasCheckConstraint("ck_payments_status", CodeChecks.In<PaymentStatus>("status"));
            t.HasCheckConstraint("ck_payments_provider", CodeChecks.In("provider", Providers));
            t.HasCheckConstraint("ck_payments_collection_mode", "collection_mode IS NULL OR " + CodeChecks.In<CollectionMode>("collection_mode"));
            t.HasCheckConstraint("ck_payments_amount", "amount_paise >= 0");
            t.HasCheckConstraint("ck_payments_refunds", "refunded_paise >= 0 AND refund_pending_paise >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.ProviderOrderId).HasMaxLength(100);
        builder.Property(x => x.ProviderPaymentId).HasMaxLength(100);
        builder.Property(x => x.CollectionReference).HasMaxLength(100);
        builder.Property(x => x.FailureCode).HasMaxLength(100);
        builder.Property(x => x.FailureMessage).HasMaxLength(500);

        builder.HasMany(x => x.Refunds).WithOne().HasForeignKey(r => r.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Refunds).HasField("_refunds").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.OrderId, "ux_payments_order_paid").IsUnique().HasFilter("status IN ('CAPTURED', 'COLLECTED') AND NOT is_excess");
        builder.HasIndex(x => x.OrderId, "ux_payments_order_delivery").IsUnique().HasFilter("provider IN ('CASH', 'UPI_ON_DELIVERY')");
        builder.HasIndex(x => x.ProviderPaymentId, "ux_payments_provider_payment_id").IsUnique().HasFilter("provider_payment_id IS NOT NULL");
        builder.HasIndex(x => new { x.Provider, x.ProviderOrderId }, "ux_payments_provider_order_id").IsUnique().HasFilter("provider_order_id IS NOT NULL");
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.Status }, "ix_payments_location_status");
        builder.HasIndex(x => new { x.CollectedByRiderId, x.CollectedAt }, "ix_payments_unsettled_cash").HasFilter("status = 'COLLECTED' AND cash_settlement_id IS NULL");
        builder.HasIndex(x => new { x.Status, x.Method }, "ix_payments_open_attempts").HasFilter("status IN ('PENDING', 'AUTHORIZED')");
    }
}

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds", t =>
        {
            t.HasCheckConstraint("ck_refunds_reason_code", CodeChecks.In<RefundReason>("reason_code"));
            t.HasCheckConstraint("ck_refunds_method", CodeChecks.In<RefundMethod>("method"));
            t.HasCheckConstraint("ck_refunds_status", CodeChecks.In<RefundStatus>("status"));
            t.HasCheckConstraint("ck_refunds_amount", "amount_paise > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProviderRefundId).HasMaxLength(100);
        builder.Property(x => x.SourceReference).HasMaxLength(200);
        builder.Property(x => x.PayeeUpiId).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.ManualReference).HasMaxLength(Payment.MaxManualReferenceLength);
        builder.Ignore(x => x.IsSystemRequested);
        builder.Ignore(x => x.HoldsAmount);
        builder.Ignore(x => x.IsPending);

        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => x.SourceReference).IsUnique().HasFilter("source_reference IS NOT NULL");
        builder.HasIndex(x => x.ProviderRefundId).IsUnique().HasFilter("provider_refund_id IS NOT NULL");
        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.Status });
    }
}

internal sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("webhook_events", t => t.HasCheckConstraint("ck_webhook_events_status", CodeChecks.In<WebhookEventStatus>("status")));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ProviderEventId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Headers).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Error).HasMaxLength(WebhookEvent.MaxErrorLength);
        builder.Ignore(x => x.CanReprocess);
        builder.HasIndex(x => new { x.Provider, x.ProviderEventId }, "ux_webhook_events_provider_event").IsUnique().HasFilter("signature_valid");
        builder.HasIndex(x => x.ReceivedAt);
        builder.HasIndex(x => x.Status).HasFilter("status = 'ERROR'");
    }
}

internal sealed class CashSettlementConfiguration : IEntityTypeConfiguration<CashSettlement>
{
    public void Configure(EntityTypeBuilder<CashSettlement> builder)
    {
        builder.ToTable("cash_settlements", t => t.HasCheckConstraint("ck_cash_settlements_status", CodeChecks.In<CashSettlementStatus>("status")));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.DisputeReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.RiderId, x.SettlementDate }, "ux_cash_settlements_rider_date").IsUnique();
        builder.HasIndex(x => new { x.FulfillmentLocationId, x.SettlementDate });
    }
}

internal sealed class ReconciliationIssueConfiguration : IEntityTypeConfiguration<ReconciliationIssue>
{
    public void Configure(EntityTypeBuilder<ReconciliationIssue> builder)
    {
        builder.ToTable("reconciliation_issues", t =>
        {
            t.HasCheckConstraint("ck_reconciliation_issues_kind", CodeChecks.In<IssueKind>("kind"));
            t.HasCheckConstraint("ck_reconciliation_issues_status", CodeChecks.In<IssueStatus>("status"));
            t.HasCheckConstraint("ck_reconciliation_issues_subject_type", CodeChecks.In<IssueSubjectType>("subject_type"));
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Details).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Kind, x.SubjectId }, "ux_reconciliation_issues_open").IsUnique().HasFilter("status = 'OPEN'");
        builder.HasIndex(x => new { x.Status, x.DetectedAt });
    }
}
