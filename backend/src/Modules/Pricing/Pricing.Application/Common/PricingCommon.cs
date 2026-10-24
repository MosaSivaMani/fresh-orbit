using System.Globalization;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Pricing.Domain.Periods;
using FreshOrbit.Pricing.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using DomainChargeBasis = FreshOrbit.Pricing.Domain.Periods.ChargeBasis;
using PublicChargeBasis = FreshOrbit.Pricing.Public.ChargeBasis;

namespace FreshOrbit.Pricing.Application.Common;

/// <summary>403 LOCATION_ACCESS_DENIED: a location-scoped staff member addressed another location.</summary>
public sealed class PricingLocationAccessDeniedException()
    : ForbiddenException(PricingErrors.LocationAccessDenied, "You do not have access to this fulfillment location.")
{
}

/// <summary>428 VERSION_REQUIRED: the schedule has periods but the request sent no <c>If-Match</c>.</summary>
public sealed class PricingVersionRequiredException()
    : DomainException(PricingErrors.VersionRequired, "Send the current version of the schedule in the If-Match header.", "If-Match")
{
}

public static class PricingRules
{
    /// <summary>D35: schedules tax rules (global GST rates); ADMIN and FINANCE only.</summary>
    public const string TaxManagePermission = "tax.manage";

    /// <summary>D35, defence in depth behind the endpoint policy: staff need <see cref="TaxManagePermission"/>.</summary>
    public static void EnsureCanManageTax(ICurrentActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.ActorType != ActorType.System && !actor.HasPermission(TaxManagePermission))
        {
            throw new ForbiddenException(ForbiddenException.DefaultCode, "You need the tax.manage permission to change tax rules.");
        }
    }

    public static void EnsureLocationAccess(ICurrentActor actor, Guid fulfillmentLocationId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.CanAccessLocation(fulfillmentLocationId))
        {
            throw new PricingLocationAccessDeniedException();
        }
    }

    public static string Version(uint version) => version.ToString(CultureInfo.InvariantCulture);

    /// <summary>The price unit of a variant: KG, PIECE or PACK.</summary>
    public static string PriceUnit(SellUnit sellUnit) => sellUnit switch
    {
        SellUnit.Kg => "KG",
        SellUnit.Piece => "PIECE",
        _ => "PACK",
    };

    public static PublicChargeBasis ToPublic(DomainChargeBasis basis) => basis switch
    {
        DomainChargeBasis.PerKg => PublicChargeBasis.PerKg,
        DomainChargeBasis.PerItem => PublicChargeBasis.PerItem,
        _ => PublicChargeBasis.Flat,
    };

    /// <summary>Change in basis points against <paramref name="previous"/> (null without a positive previous value).</summary>
    public static int? ChangeBp(long? previous, long next)
        => previous is > 0 and { } before ? (int)Math.Clamp((next - before) * 10_000 / before, int.MinValue, int.MaxValue) : null;
}

/// <summary>The latest period of a schedule versions it; check the request's precondition against it (PRC-R16).</summary>
public static class SchedulePreconditions
{
    /// <summary>Returns the row whose version must be checked and bumped, or null when the schedule is empty.</summary>
    public static T? Check<T>(IReadOnlyList<T> periods, Contracts.SchedulePrecondition precondition, Action<T, uint> setExpectedVersion)
        where T : PricePeriod
    {
        ArgumentNullException.ThrowIfNull(periods);
        ArgumentNullException.ThrowIfNull(precondition);
        ArgumentNullException.ThrowIfNull(setExpectedVersion);
        var latest = Latest(periods);
        if (latest is null)
        {
            return precondition.IfMatch is { } stale ? throw new ConcurrencyException(stale, 0) : null;
        }

        if (precondition.IfNoneMatchAny)
        {
            throw new ConcurrencyException(0, latest.Version);
        }

        setExpectedVersion(latest, precondition.IfMatch ?? throw new PricingVersionRequiredException());
        return latest;
    }

    public static T? Latest<T>(IEnumerable<T> periods)
        where T : PricePeriod
        => periods.OrderByDescending(p => p.ValidFrom).ThenByDescending(p => p.Id).FirstOrDefault();
}
