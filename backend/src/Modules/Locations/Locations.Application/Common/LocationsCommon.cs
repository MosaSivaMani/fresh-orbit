using System.Globalization;
using FreshOrbit.Locations.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Locations.Application.Common;

/// <summary>The 60-second pincode resolution cache (LOC-R16); commands clear it after they save.</summary>
public interface IPincodeResolutionCache
{
    void Invalidate();
}

/// <summary>403 LOCATION_ACCESS_DENIED: a location-scoped staff member addressed another location.</summary>
public sealed class LocationAccessDeniedException()
    : DomainException(LocationsErrors.LocationAccessDenied, "You do not have access to this fulfillment location.")
{
}

public static class LocationAccess
{
    public static void Ensure(ICurrentActor actor, Guid fulfillmentLocationId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.CanAccessLocation(fulfillmentLocationId))
        {
            throw new LocationAccessDeniedException();
        }
    }
}

/// <summary>Formats of values in requests and responses.</summary>
public static class Formats
{
    public const string TimeOfDay = "HH:mm";

    public static string Version(uint version) => version.ToString(CultureInfo.InvariantCulture);

    public static string Time(TimeOnly time) => time.ToString(TimeOfDay, CultureInfo.InvariantCulture);

    public static bool TryParseTime(string? value, out TimeOnly time)
        => TimeOnly.TryParseExact(value, TimeOfDay, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
