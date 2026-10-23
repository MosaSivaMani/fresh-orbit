using FreshOrbit.Reporting.Domain;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Reporting.Application;

/// <summary>A <c>fulfillmentLocationId</c> outside the caller's access (HTTP 403 <c>LOCATION_ACCESS_DENIED</c>, REP-R11).</summary>
public sealed class LocationAccessDeniedException()
    : DomainException(ReportingErrors.LocationAccessDenied, "You do not have access to this fulfillment location.");

/// <summary>Common report query parameters (REP-R10, R11, R12).</summary>
public sealed record ReportQuery(
    DateOnly? FromDate,
    DateOnly? ToDate,
    Guid? CityId,
    Guid? FulfillmentLocationId,
    int Page,
    int PageSize,
    string? Sort,
    bool Csv,
    IReadOnlyDictionary<string, string> Extra);

/// <summary>The REP-R10 response envelope. Money in paise; <see cref="AsOf"/> = oldest refresh of the views used.</summary>
public sealed record ReportResponse(
    DateTimeOffset? AsOf,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyDictionary<string, object?> Filters,
    IReadOnlyDictionary<string, object?> Totals,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>Report date range rules (REP-R10).</summary>
public static class ReportPeriod
{
    public const int DefaultDays = 30;
    public const int MaxDays = 366;

    /// <summary>Defaults <c>toDate</c> to <paramref name="today"/> and <c>fromDate</c> to <c>toDate − 29</c>; refuses inverted or over-long ranges.</summary>
    public static (DateOnly From, DateOnly To) Resolve(DateOnly? fromDate, DateOnly? toDate, DateOnly today)
    {
        var to = toDate ?? today;
        var from = fromDate ?? to.AddDays(-(DefaultDays - 1));
        if (from > to)
        {
            throw new RefusalException([new FieldError("fromDate", ReportingErrors.ValidationFailed, "From date must not be after to date.")]);
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxDays)
        {
            throw new RefusalException([new FieldError("toDate", ReportingErrors.ValidationFailed, $"The range must be at most {MaxDays} days.")]);
        }

        return (from, to);
    }
}

/// <summary>Location scoping of report rows (REP-R11).</summary>
public static class ReportScope
{
    /// <summary>
    /// The locations whose rows the caller may see, or <c>null</c> for every location. A requested location outside
    /// access → 403; a city narrows to its locations ∩ access; a location not in the requested city → 422.
    /// </summary>
    /// <param name="cityLocations">Locations of <paramref name="cityId"/> (ignored when no city is requested).</param>
    public static IReadOnlyList<Guid>? Resolve(ICurrentActor actor, Guid? locationId, Guid? cityId, IReadOnlyCollection<Guid> cityLocations)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(cityLocations);
        if (locationId is { } location)
        {
            if (!actor.IsAllLocations && !actor.LocationIds.Contains(location))
            {
                throw new LocationAccessDeniedException();
            }

            if (cityId is not null && !cityLocations.Contains(location))
            {
                throw new RefusalException([new FieldError("fulfillmentLocationId", ReportingErrors.ValidationFailed, "The fulfillment location is not in the requested city.")]);
            }

            return [location];
        }

        if (cityId is not null)
        {
            return actor.IsAllLocations ? [.. cityLocations] : [.. cityLocations.Where(actor.LocationIds.Contains)];
        }

        return actor.IsAllLocations ? null : [.. actor.LocationIds];
    }
}
