using FreshOrbit.Audit.Domain.Entries;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Audit.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        var actorTypes = string.Join(", ", EnumCodes.AllCodes<ActorType>().Select(c => $"'{c}'"));
        builder.ToTable("entries", t => t.HasCheckConstraint("ck_entries_actor_type", $"actor_type IN ({actorTypes})"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Action).HasMaxLength(80).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(AuditEntry.MaxEntityTypeLength).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(AuditEntry.MaxEntityIdLength).IsRequired();
        builder.Property(x => x.ActorName).HasMaxLength(200);
        builder.Property(x => x.Before).HasColumnType("jsonb");
        builder.Property(x => x.After).HasColumnType("jsonb");
        builder.Property(x => x.Ip).HasMaxLength(45);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => x.ActorId);
    }
}
