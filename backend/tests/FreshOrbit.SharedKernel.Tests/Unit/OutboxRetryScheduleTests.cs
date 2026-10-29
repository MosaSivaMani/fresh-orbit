using AwesomeAssertions;
using FreshOrbit.SharedKernel.EntityFramework.Outbox;

namespace FreshOrbit.SharedKernel.Tests.Unit;

/// <summary>BE-2 / D296: exponential backoff with jitter, and the Outbox configuration refused when missing.</summary>
public sealed class OutboxRetryScheduleTests
{
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Max = TimeSpan.FromHours(1);

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(4, 80)]
    [InlineData(9, 2560)]
    [InlineData(10, 3600)]
    [InlineData(15, 3600)]
    [InlineData(100, 3600)]
    public void Delay_doubles_after_each_failure_up_to_the_cap(int failedAttempts, int expectedSeconds)
    {
        OutboxRetrySchedule.ExponentialDelay(failedAttempts, Base, Max).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
        OutboxRetrySchedule.DelayAfter(failedAttempts, Base, Max, jitterSample: 0.5).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void Jitter_spreads_the_delay_by_twenty_percent_and_never_exceeds_the_cap()
    {
        OutboxRetrySchedule.DelayAfter(3, Base, Max, jitterSample: 0).Should().Be(TimeSpan.FromSeconds(32));
        OutboxRetrySchedule.DelayAfter(3, Base, Max, jitterSample: 1).Should().Be(TimeSpan.FromSeconds(48));
        OutboxRetrySchedule.DelayAfter(12, Base, Max, jitterSample: 0).Should().Be(TimeSpan.FromMinutes(48));
        OutboxRetrySchedule.DelayAfter(12, Base, Max, jitterSample: 1).Should().Be(Max);
        OutboxRetrySchedule.DelayAfter(3, Base, Max, jitterSample: 7).Should().Be(TimeSpan.FromSeconds(48), "the sample is clamped to [0, 1]");
    }

    [Fact]
    public void Attempts_start_at_one()
    {
        var act = () => OutboxRetrySchedule.DelayAfter(0, Base, Max, 0.5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Worker_settings_ride_out_an_outage_of_about_six_hours()
    {
        var options = new OutboxOptions { BatchSize = 50, MaxAttempts = 15, PollInterval = TimeSpan.FromSeconds(5), RetryBaseDelay = Base, RetryMaxDelay = Max };

        // 10+20+…+2560 s (9 delays) + 5 × 3600 s = 23 110 s before the 15th attempt.
        OutboxRetrySchedule.RetryWindow(options).Should().Be(TimeSpan.FromSeconds(23_110));
        OutboxOptionsValidator.Failures(options).Should().BeEmpty();
    }

    [Fact]
    public void Missing_outbox_configuration_is_refused_for_every_key()
    {
        var failures = OutboxOptionsValidator.Failures(new OutboxOptions());

        failures.Should().HaveCount(5);
        failures.Should().Contain(f => f.StartsWith("Outbox:BatchSize", StringComparison.Ordinal));
        failures.Should().Contain(f => f.StartsWith("Outbox:MaxAttempts", StringComparison.Ordinal));
        failures.Should().Contain(f => f.StartsWith("Outbox:PollInterval", StringComparison.Ordinal));
        failures.Should().Contain(f => f.StartsWith("Outbox:RetryBaseDelay", StringComparison.Ordinal));
        failures.Should().Contain(f => f.StartsWith("Outbox:RetryMaxDelay", StringComparison.Ordinal));
        new OutboxOptionsValidator().Validate(null, new OutboxOptions()).Failed.Should().BeTrue();
    }

    [Fact]
    public void Cap_below_the_base_delay_or_above_one_day_is_refused()
    {
        var below = new OutboxOptions { BatchSize = 50, MaxAttempts = 15, PollInterval = TimeSpan.FromSeconds(5), RetryBaseDelay = TimeSpan.FromMinutes(5), RetryMaxDelay = TimeSpan.FromMinutes(1) };
        var above = new OutboxOptions { BatchSize = 50, MaxAttempts = 15, PollInterval = TimeSpan.FromSeconds(5), RetryBaseDelay = Base, RetryMaxDelay = TimeSpan.FromDays(2) };

        OutboxOptionsValidator.Failures(below).Should().ContainSingle().Which.Should().StartWith("Outbox:RetryMaxDelay");
        OutboxOptionsValidator.Failures(above).Should().ContainSingle().Which.Should().StartWith("Outbox:RetryMaxDelay");
    }
}
