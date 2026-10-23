using FreshOrbit.Delivery.Public;
using FreshOrbit.Delivery.Domain.Shared;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Delivery.Application.Common;

/// <summary>
/// What Delivery needs from Locations: locations (status, city, time zone) and closed dates. Implemented in
/// Infrastructure over <c>Locations.Public</c>; tests substitute a fake.
/// </summary>
public interface ILocationCalendar
{
    /// <summary>The requested locations that exist (missing ids are absent).</summary>
    Task<IReadOnlyList<FulfillmentLocationSummary>> GetLocationsAsync(IReadOnlyCollection<Guid> fulfillmentLocationIds, CancellationToken cancellationToken);

    /// <summary>Local dates in <c>[fromDate, toDate]</c> on which the location is closed.</summary>
    Task<IReadOnlySet<DateOnly>> GetClosedDatesAsync(Guid fulfillmentLocationId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
}

/// <summary>Delivery limits from the order policy (A11: <c>max_delivery_attempts</c>, default 3).</summary>
public interface IDeliveryPolicyProvider
{
    Task<int> GetMaxDeliveryAttemptsAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken);
}

/// <summary>
/// Max delivery attempts from the order policy of the location's city through <see cref="IDeliveryAttemptPolicy"/>
/// (implemented by Settings). No implementation registered = configuration missing (CLAUDE.md rule 7).
/// </summary>
public sealed class OrderPolicyDeliveryPolicyProvider(IEnumerable<IDeliveryAttemptPolicy> policies, ILocationCalendar calendar) : IDeliveryPolicyProvider
{
    public async Task<int> GetMaxDeliveryAttemptsAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken)
    {
        var policy = policies.FirstOrDefault()
            ?? throw new DomainException(DeliveryErrors.ConfigurationMissing, "No order policy source is registered; max delivery attempts cannot be read.");
        var location = await calendar.GetRequiredLocationAsync(fulfillmentLocationId, "fulfillmentLocationId", cancellationToken);
        return await policy.GetMaxDeliveryAttemptsAsync(location.CityId, cancellationToken);
    }
}

public static class LocationCalendarExtensions
{
    /// <summary>The location, or a 422 on <paramref name="field"/> when it does not exist.</summary>
    public static async Task<FulfillmentLocationSummary> GetRequiredLocationAsync(this ILocationCalendar calendar, Guid fulfillmentLocationId, string field, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        var locations = await calendar.GetLocationsAsync([fulfillmentLocationId], cancellationToken);
        return locations.FirstOrDefault(l => l.Id == fulfillmentLocationId)
            ?? throw DeliveryErrors.Invalid(field, "The fulfillment location does not exist.");
    }

    public static TimeZoneInfo TimeZone(this FulfillmentLocationSummary location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone);
    }
}

public static class ActorScope
{
    /// <summary>DEL-R8: 403 LOCATION_ACCESS_DENIED outside the staff member's locations.</summary>
    public static void EnsureLocation(this ICurrentActor actor, Guid fulfillmentLocationId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.CanAccessLocation(fulfillmentLocationId))
        {
            throw new LocationAccessDeniedException();
        }
    }

    /// <summary>Locations a list is filtered to; null = all.</summary>
    public static IReadOnlyCollection<Guid>? ScopedLocations(this ICurrentActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.ActorType == ActorType.System || actor.IsAllLocations ? null : [.. actor.LocationIds];
    }
}
