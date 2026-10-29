namespace FreshOrbit.SharedKernel.EntityFramework.Outbox;

/// <summary>
/// When a failed outbox row is tried again (BE-2, D296): exponential backoff with jitter. After the n-th failed attempt
/// the delay is <c>RetryBaseDelay × 2^(n−1)</c>, capped at <c>RetryMaxDelay</c>, then spread by ±<see cref="JitterFraction"/>
/// (and never above the cap) so rows that failed together during an outage do not all retry in the same poll.
/// </summary>
public static class OutboxRetrySchedule
{
    /// <summary>Jitter spread: the delay is multiplied by a factor between 0.8 and 1.2.</summary>
    public const double JitterFraction = 0.2;

    /// <summary>The delay before the next attempt after <paramref name="failedAttempts"/> failures.</summary>
    /// <param name="failedAttempts">Failed attempts so far (1 or more).</param>
    /// <param name="baseDelay">Delay after the first failure.</param>
    /// <param name="maxDelay">The cap.</param>
    /// <param name="jitterSample">A uniform sample in [0, 1]; 0.5 gives the exact exponential delay.</param>
    public static TimeSpan DelayAfter(int failedAttempts, TimeSpan baseDelay, TimeSpan maxDelay, double jitterSample)
    {
        var exponential = ExponentialDelay(failedAttempts, baseDelay, maxDelay);
        var factor = 1 - JitterFraction + (2 * JitterFraction * Math.Clamp(jitterSample, 0, 1));
        var ticks = Math.Min(Math.Round(exponential.Ticks * factor), maxDelay.Ticks);
        return TimeSpan.FromTicks((long)ticks);
    }

    /// <summary>The delay without jitter: <c>base × 2^(n−1)</c>, capped at <paramref name="maxDelay"/>.</summary>
    public static TimeSpan ExponentialDelay(int failedAttempts, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failedAttempts, 1);
        var exponent = Math.Min(failedAttempts - 1, 40);
        var ticks = Math.Min(baseDelay.Ticks * Math.Pow(2, exponent), maxDelay.Ticks);
        return TimeSpan.FromTicks((long)ticks);
    }

    /// <summary>
    /// How long a handler may keep failing before its row is dead-lettered, without jitter: the sum of the delays
    /// between the first and the last of <see cref="OutboxOptions.MaxAttempts"/> attempts.
    /// </summary>
    public static TimeSpan RetryWindow(OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var total = TimeSpan.Zero;
        for (var failed = 1; failed < options.MaxAttempts; failed++)
        {
            total += ExponentialDelay(failed, options.RetryBaseDelay, options.RetryMaxDelay);
        }

        return total;
    }
}
