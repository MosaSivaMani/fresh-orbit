using AwesomeAssertions;
using FreshOrbit.SharedKernel.Time;
using FreshOrbit.Testing;

namespace FreshOrbit.SharedKernel.Tests.Unit;

public sealed class StoredPrecisionTests
{
    private static readonly DateTimeOffset Whole = new(2026, 10, 24, 10, 15, 7, TimeSpan.Zero);

    [Fact]
    public void Truncating_drops_only_the_sub_microsecond_ticks()
    {
        var value = Whole.AddTicks(4_932_801);

        var truncated = value.TruncateToMicroseconds();

        truncated.Should().Be(Whole.AddTicks(4_932_800));
        truncated.TruncateToMicroseconds().Should().Be(truncated);
    }

    [Fact]
    public void The_system_and_test_clocks_keep_whole_microseconds()
    {
        (new SystemClock().UtcNow.Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);

        var clock = new TestClock(Whole.AddTicks(3));
        clock.UtcNow.Should().Be(Whole);
        clock.Advance(TimeSpan.FromTicks(15));
        clock.UtcNow.Should().Be(Whole.AddTicks(10));
    }
}
