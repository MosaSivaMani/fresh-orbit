namespace FreshOrbit.SharedKernel.EntityFramework.Outbox;

/// <summary>Configuration section <c>Outbox</c>.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Rows claimed per batch.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>After this many failed attempts the failing handlers get a dead letter and the row is no longer picked.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Pause between polls when the previous batch was not full.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
}
