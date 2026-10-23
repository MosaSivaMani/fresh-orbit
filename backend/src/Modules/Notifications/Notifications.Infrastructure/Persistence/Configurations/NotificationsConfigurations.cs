using System.Text.Json;
using FreshOrbit.Notifications.Domain.Devices;
using FreshOrbit.Notifications.Domain.Messages;
using FreshOrbit.Notifications.Domain.Preferences;
using FreshOrbit.Notifications.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshOrbit.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("templates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200);
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Variables).HasColumnType("text[]").IsRequired();
        builder.Property(x => x.DltTemplateId).HasMaxLength(40);
        builder.HasIndex(x => new { x.Code, x.Channel }).IsUnique();
    }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TemplateCode).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200);
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Payload)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>(),
                new ValueComparer<Dictionary<string, string>>(
                    (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                    v => v.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key.GetHashCode(StringComparison.Ordinal), pair.Value.GetHashCode(StringComparison.Ordinal))),
                    v => new Dictionary<string, string>(v)))
            .IsRequired();
        builder.Property(x => x.ProviderMessageId).HasMaxLength(200);
        builder.Property(x => x.LastError).HasMaxLength(500);
        builder.Property(x => x.DedupeKey).HasMaxLength(300);
        builder.HasIndex(x => x.DedupeKey).IsUnique().HasFilter("dedupe_key IS NOT NULL");
        builder.HasIndex(x => new { x.UserId, x.QueuedAt }).IsDescending(false, true);
        builder.HasIndex(x => x.QueuedAt).HasFilter("status = 'QUEUED'").HasDatabaseName("ix_messages_queued");
    }
}

internal sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PushToken).HasMaxLength(DeviceToken.MaxTokenLength).IsRequired();
        builder.HasIndex(x => x.PushToken).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

internal sealed class PreferenceConfiguration : IEntityTypeConfiguration<Preference>
{
    public void Configure(EntityTypeBuilder<Preference> builder)
    {
        builder.ToTable("preferences");
        builder.HasKey(x => new { x.UserId, x.Category, x.Channel });
    }
}
