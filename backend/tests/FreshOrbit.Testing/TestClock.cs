using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Testing;

/// <summary>
/// A clock tests control. Starts at the real current time (so issued JWTs are valid). Like <see cref="SystemClock"/>
/// it keeps whole microseconds, the precision Postgres stores.
/// </summary>
public sealed class TestClock : IClock
{
    private DateTimeOffset _utcNow;

    public TestClock()
        : this(DateTimeOffset.UtcNow)
    {
    }

    public TestClock(DateTimeOffset utcNow) => Set(utcNow);

    public DateTimeOffset UtcNow => _utcNow;

    public void Set(DateTimeOffset utcNow) => _utcNow = utcNow.ToUniversalTime().TruncateToMicroseconds();

    public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by).TruncateToMicroseconds();
}
