namespace FreshOrbit.Delivery.Domain.Templates;

public interface ISlotTemplateRepository
{
    Task<SlotTemplate?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<SlotTemplate>> GetActiveByLocationAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken);

    /// <summary>Distinct locations that have at least one active template.</summary>
    Task<IReadOnlyList<Guid>> GetLocationIdsWithActiveTemplatesAsync(CancellationToken cancellationToken);

    void Add(SlotTemplate slotTemplate);
}
