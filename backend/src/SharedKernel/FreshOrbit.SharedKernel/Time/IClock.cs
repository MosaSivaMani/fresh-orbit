namespace FreshOrbit.SharedKernel.Time;

/// <summary>
/// The current time in UTC, at the precision the database stores (whole microseconds, see
/// <see cref="StoredPrecision"/>). Inject it instead of reading <see cref="DateTimeOffset.UtcNow"/>.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>The real clock, cut to whole microseconds so a time read back from Postgres equals the one written.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow.TruncateToMicroseconds();
}
