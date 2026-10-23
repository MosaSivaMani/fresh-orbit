namespace FreshOrbit.Delivery.Domain.Slots;

public interface IDeliverySlotRepository
{
    Task<DeliverySlot?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Slots of the location with <c>from &lt;= start_at &lt; until</c> (tracked).</summary>
    Task<IReadOnlyList<DeliverySlot>> GetStartingBetweenAsync(Guid fulfillmentLocationId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeliverySlot>> GetByLocalDateAsync(Guid fulfillmentLocationId, DateOnly slotDate, CancellationToken cancellationToken);

    /// <summary>Slots of the location starting after <paramref name="after"/> (tracked).</summary>
    Task<IReadOnlyList<DeliverySlot>> GetFutureByLocationAsync(Guid fulfillmentLocationId, DateTimeOffset after, CancellationToken cancellationToken);

    void AddRange(IEnumerable<DeliverySlot> slots);
}
