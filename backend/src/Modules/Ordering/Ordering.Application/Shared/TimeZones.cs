namespace FreshOrbit.Ordering.Application.Shared;

/// <summary>Resolves the IANA time zone stored with a location; UTC when missing or unknown.</summary>
public static class TimeZones
{
    public static TimeZoneInfo Find(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) ? zone : TimeZoneInfo.Utc;
    }
}
