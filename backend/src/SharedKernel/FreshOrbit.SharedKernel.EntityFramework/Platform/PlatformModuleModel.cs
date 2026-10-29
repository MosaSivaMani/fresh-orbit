using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.SharedKernel.EntityFramework.Platform;

/// <summary>Tables of the <c>platform</c> schema: outbox, inbox, dead letters, idempotency keys, sample records.</summary>
public sealed class PlatformModuleModel : IModuleModel
{
    public const string SchemaName = "platform";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox", SchemaName);
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.EventType).HasMaxLength(OutboxMessage.MaxEventTypeLength).IsRequired();
            b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.CorrelationId).HasMaxLength(100);
            b.HasIndex(x => x.OccurredAt).HasFilter("processed_at IS NULL");
        });

        modelBuilder.Entity<InboxRecord>(b =>
        {
            b.ToTable("inbox", SchemaName);
            b.HasKey(x => new { x.EventId, x.Handler });
            b.Property(x => x.Handler).HasMaxLength(OutboxMessage.MaxEventTypeLength);
        });

        modelBuilder.Entity<DeadLetter>(b =>
        {
            b.ToTable("dead_letters", SchemaName);
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Handler).HasMaxLength(OutboxMessage.MaxEventTypeLength).IsRequired();
            b.Property(x => x.Error).IsRequired();
            b.HasIndex(x => x.EventId);
            b.HasIndex(x => x.FailedAt).HasFilter("replayed_at IS NULL");
        });

        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.ToTable("idempotency_keys", SchemaName);
            b.HasKey(x => new { x.Scope, x.Key });
            b.Property(x => x.Scope).HasMaxLength(300);
            b.Property(x => x.Key).HasMaxLength(100);
            b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
            b.Property(x => x.Response).HasColumnType("jsonb");
            b.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<SampleRecord>(b =>
        {
            b.ToTable("sample_records", SchemaName);
            b.HasKey(x => new { x.TableName, x.RecordId });
            b.Property(x => x.TableName).HasMaxLength(130);
        });
    }
}
