namespace FreshOrbit.SharedKernel.Time;

/// <summary>
/// Postgres <c>timestamptz</c> keeps whole microseconds; .NET keeps 100 ns ticks (Linux clocks fill them). A time
/// that is compared after a round trip (a lockout end, a period start) must be cut to microseconds before it is
/// used, or the value read back differs from the one in memory.
/// </summary>
public static class StoredPrecision
{
    /// <summary>The same instant without its sub-microsecond ticks (the value Postgres stores).</summary>
    public static DateTimeOffset TruncateToMicroseconds(this DateTimeOffset value)
        => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));
}
