using FreshOrbit.Inventory.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Inventory.Application.Common;

/// <summary>
/// The command's single save, plus what Inventory commands need around it: clearing tracked changes before a retry
/// from Read, and translation of constraint violations (unique batch code, unique stock item) into refusals.
/// </summary>
public interface IInventoryUnitOfWork : IUnitOfWork
{
    /// <summary>Forgets every tracked change (before re-running a command from Read after a lost race).</summary>
    void ClearChanges();
}

/// <summary>Staff without access to the location (HTTP 403 LOCATION_ACCESS_DENIED).</summary>
public sealed class LocationAccessDeniedException : ForbiddenException
{
    public LocationAccessDeniedException()
        : base(InventoryErrors.LocationAccessDenied, "You do not have access to this fulfillment location.", "fulfillmentLocationId")
    {
    }
}

public static class LocationScope
{
    /// <summary>Refuses with <see cref="LocationAccessDeniedException"/> when the actor may not act on the location (INV-R10).</summary>
    public static void Ensure(ICurrentActor actor, Guid fulfillmentLocationId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.CanAccessLocation(fulfillmentLocationId))
        {
            throw new LocationAccessDeniedException();
        }
    }
}

/// <summary>INV-R23: commands without If-Match re-run from Read after a lost race, at most three times.</summary>
public static class SaveRetry
{
    public const int MaxAttempts = 3;

    public static async Task<T> RunAsync<T>(IInventoryUnitOfWork unitOfWork, Func<Task<T>> command)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(command);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await command();
            }
            catch (ConcurrentSaveException) when (attempt < MaxAttempts)
            {
                unitOfWork.ClearChanges();
            }
        }
    }
}
