using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Testing;

/// <summary>A clock tests control. Starts at the real current time (so issued JWTs are valid).</summary>
public sealed class TestClock : IClock
{
    private DateTimeOffset _utcNow;

    public TestClock()
        : this(DateTimeOffset.UtcNow)
    {
    }

    public TestClock(DateTimeOffset utcNow) => _utcNow = utcNow.ToUniversalTime();

    public DateTimeOffset UtcNow => _utcNow;

    public void Set(DateTimeOffset utcNow) => _utcNow = utcNow.ToUniversalTime();

    public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);
}
