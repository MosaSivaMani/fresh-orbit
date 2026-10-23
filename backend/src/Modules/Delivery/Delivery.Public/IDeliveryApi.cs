namespace FreshOrbit.Delivery.Public;

/// <summary>What other modules may ask the Delivery module.</summary>
public interface IDeliveryApi
{
    /// <summary>Open slots starting within [from, until) whose cutoff has not passed and that have capacity left.</summary>
    Task<IReadOnlyList<SlotInfo>> GetOpenSlotsAsync(Guid fulfillmentLocationId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken = default);

    /// <summary>
    /// Books one unit of slot capacity. Tracked on the shared DbContext and saved by the caller; never saves.
    /// </summary>
    Task<SlotBooking> BookSlotAsync(Guid slotId, Guid fulfillmentLocationId, CancellationToken cancellationToken = default);
}

public sealed record SlotInfo(Guid SlotId, DateTimeOffset StartAt, DateTimeOffset EndAt, DateTimeOffset CutoffAt, int Remaining);

/// <param name="RefusalCode">UPPER_SNAKE reason when not booked (e.g. SLOT_FULL, SLOT_CUTOFF_PASSED).</param>
public sealed record SlotBooking(bool Succeeded, Guid SlotId, DateTimeOffset? StartAt, DateTimeOffset? EndAt, string? RefusalCode);
