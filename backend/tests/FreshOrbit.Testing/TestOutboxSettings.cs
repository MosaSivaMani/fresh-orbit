using FreshOrbit.SharedKernel.EntityFramework.Outbox;

namespace FreshOrbit.Testing;

/// <summary>
/// The <c>Outbox</c> configuration of the test hosts. Every value is required (D296), so the Api factory and the
/// Worker-like service provider both set them. Five attempts, 10 s first delay, 1 h cap.
/// </summary>
public static class TestOutboxSettings
{
    public const int MaxAttempts = 5;

    public static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan RetryMaxDelay = TimeSpan.FromHours(1);

    public static IReadOnlyDictionary<string, string> Values { get; } = new Dictionary<string, string>
    {
        [$"{OutboxOptions.SectionName}:{nameof(OutboxOptions.BatchSize)}"] = "100",
        [$"{OutboxOptions.SectionName}:{nameof(OutboxOptions.MaxAttempts)}"] = "5",
        [$"{OutboxOptions.SectionName}:{nameof(OutboxOptions.PollInterval)}"] = "00:00:01",
        [$"{OutboxOptions.SectionName}:{nameof(OutboxOptions.RetryBaseDelay)}"] = "00:00:10",
        [$"{OutboxOptions.SectionName}:{nameof(OutboxOptions.RetryMaxDelay)}"] = "01:00:00",
    };
}
