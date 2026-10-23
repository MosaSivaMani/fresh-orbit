namespace FreshOrbit.SharedKernel.Time;

/// <summary>The current time in UTC. Inject it instead of reading <see cref="DateTimeOffset.UtcNow"/>.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
