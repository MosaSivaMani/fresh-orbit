namespace FreshOrbit.Identity.Domain.Users;

/// <summary>
/// D33 staff lockout: <see cref="MaxFailures"/> consecutive wrong passwords lock the account for
/// <see cref="FirstLock"/>, doubled for every further lock since the last successful sign-in, never longer than
/// <see cref="MaxLock"/>.
/// </summary>
public sealed record LockoutPolicy(int MaxFailures, TimeSpan FirstLock, TimeSpan MaxLock)
{
    /// <summary>Length of lock number <paramref name="lockoutCount"/> (1 = the first): first × 2^(n−1), capped.</summary>
    public TimeSpan LengthOf(int lockoutCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lockoutCount, 1);
        var length = FirstLock;
        for (var i = 1; i < lockoutCount && length < MaxLock; i++)
        {
            length += length;
        }

        return length < MaxLock ? length : MaxLock;
    }
}
