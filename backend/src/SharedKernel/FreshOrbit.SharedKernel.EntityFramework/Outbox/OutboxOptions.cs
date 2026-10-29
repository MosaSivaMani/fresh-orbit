using Microsoft.Extensions.Options;

namespace FreshOrbit.SharedKernel.EntityFramework.Outbox;

/// <summary>
/// Configuration section <c>Outbox</c>. Every value is required (rule 7): there are no defaults, and the hosts that run
/// the dispatcher (Worker, Demo Api) refuse to start while one is missing or out of range (<see cref="OutboxOptionsValidator"/>).
/// </summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Rows claimed per batch (1–1000).</summary>
    public int BatchSize { get; set; }

    /// <summary>
    /// Failed attempts after which the failing handlers get a dead letter and the row is no longer picked (1–100).
    /// With <see cref="RetryBaseDelay"/> and <see cref="RetryMaxDelay"/> it sets how long an outage the outbox rides out
    /// (<see cref="OutboxRetrySchedule.RetryWindow"/>).
    /// </summary>
    public int MaxAttempts { get; set; }

    /// <summary>Pause between polls when the previous batch was not full (more than zero).</summary>
    public TimeSpan PollInterval { get; set; }

    /// <summary>Delay after the first failed attempt; it doubles after each further failure (more than zero, D296).</summary>
    public TimeSpan RetryBaseDelay { get; set; }

    /// <summary>The longest delay between two attempts (at least <see cref="RetryBaseDelay"/>, at most one day).</summary>
    public TimeSpan RetryMaxDelay { get; set; }
}

/// <summary>Refuses a missing or out-of-range <c>Outbox</c> value (rule 7: no silent fallback).</summary>
public sealed class OutboxOptionsValidator : IValidateOptions<OutboxOptions>
{
    public const int MaxBatchSize = 1000;
    public const int MaxMaxAttempts = 100;

    public static readonly TimeSpan MaxRetryDelayLimit = TimeSpan.FromDays(1);

    public ValidateOptionsResult Validate(string? name, OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = Failures(options);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>One sentence per invalid value, naming the configuration key to fix.</summary>
    public static IReadOnlyList<string> Failures(OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.BatchSize is < 1 or > MaxBatchSize)
        {
            failures.Add($"Outbox:BatchSize must be between 1 and {MaxBatchSize}.");
        }

        if (options.MaxAttempts is < 1 or > MaxMaxAttempts)
        {
            failures.Add($"Outbox:MaxAttempts must be between 1 and {MaxMaxAttempts}.");
        }

        if (options.PollInterval <= TimeSpan.Zero)
        {
            failures.Add("Outbox:PollInterval must be more than zero.");
        }

        if (options.RetryBaseDelay <= TimeSpan.Zero)
        {
            failures.Add("Outbox:RetryBaseDelay must be more than zero.");
        }

        if (options.RetryMaxDelay < options.RetryBaseDelay || options.RetryMaxDelay > MaxRetryDelayLimit)
        {
            failures.Add("Outbox:RetryMaxDelay must be at least Outbox:RetryBaseDelay and at most one day.");
        }

        return failures;
    }
}
