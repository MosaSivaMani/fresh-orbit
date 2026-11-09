using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Delivery.Domain.Costs;

/// <summary>
/// What one delivered order costs a fulfillment location beyond the fish (D365): the rider pay per drop and the
/// packaging (insulated bag, ice, label) per order. Staff enter both in the admin; a value that is null is
/// <b>not configured</b> and is recorded as such on the shipment, never guessed (rule 7). One row per location.
/// </summary>
/// <remarks>
/// Only the per-drop rider pay model exists today (D368). A per-shift or salaried model later adds a pay-model code
/// and a daily allocation; the shipment snapshot and the report columns stay the same.
/// </remarks>
public sealed class LocationCostRates : AggregateRoot
{
    /// <summary>₹1,000 per drop: far above any rider pay per drop, low enough to catch a rupee/paise mix-up.</summary>
    public const long MaxDeliveryCostPerDropPaise = 100_000;

    /// <summary>₹500 per order: far above a bag, ice and label, low enough to catch a rupee/paise mix-up.</summary>
    public const long MaxPackagingCostPerOrderPaise = 50_000;

    private LocationCostRates()
    {
    }

    private LocationCostRates(Guid fulfillmentLocationId)
    {
        FulfillmentLocationId = fulfillmentLocationId;
    }

    public Guid FulfillmentLocationId { get; private set; }

    /// <summary>Rider pay for one delivered order, in paise; null = not configured.</summary>
    public long? DeliveryCostPerDropPaise { get; private set; }

    /// <summary>Packaging and ice for one delivered order, in paise; null = not configured.</summary>
    public long? PackagingCostPerOrderPaise { get; private set; }

    public static LocationCostRates Create(Guid fulfillmentLocationId, long? deliveryCostPerDropPaise, long? packagingCostPerOrderPaise)
    {
        var rates = new LocationCostRates(fulfillmentLocationId);
        rates.Update(deliveryCostPerDropPaise, packagingCostPerOrderPaise);
        return rates;
    }

    public void Update(long? deliveryCostPerDropPaise, long? packagingCostPerOrderPaise)
    {
        var errors = Validate(deliveryCostPerDropPaise, packagingCostPerOrderPaise);
        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        DeliveryCostPerDropPaise = deliveryCostPerDropPaise;
        PackagingCostPerOrderPaise = packagingCostPerOrderPaise;
    }

    /// <summary>The costs a shipment delivered now records (D366).</summary>
    public OrderCostSnapshot Snapshot() => new(DeliveryCostPerDropPaise, PackagingCostPerOrderPaise);

    public static List<FieldError> Validate(long? deliveryCostPerDropPaise, long? packagingCostPerOrderPaise)
    {
        var errors = new List<FieldError>();
        if (deliveryCostPerDropPaise is < 0 or > MaxDeliveryCostPerDropPaise)
        {
            errors.Add(new FieldError("deliveryCostPerDropPaise", RefusalException.DefaultCode, "The deliveryCostPerDropPaise must be between 0 and 100000 paise (₹1,000), or empty when not known."));
        }

        if (packagingCostPerOrderPaise is < 0 or > MaxPackagingCostPerOrderPaise)
        {
            errors.Add(new FieldError("packagingCostPerOrderPaise", RefusalException.DefaultCode, "The packagingCostPerOrderPaise must be between 0 and 50000 paise (₹500), or empty when not known."));
        }

        return errors;
    }
}

/// <summary>
/// The delivery and packaging cost recorded on a shipment when it is delivered (D366). A null part means the
/// location had no such cost configured at that moment; the report says "not configured" instead of a number.
/// </summary>
public sealed record OrderCostSnapshot(long? DeliveryCostPaise, long? PackagingCostPaise)
{
    /// <summary>The location has no cost rates at all.</summary>
    public static OrderCostSnapshot NotConfigured { get; } = new(null, null);
}

public interface ILocationCostRatesRepository
{
    /// <summary>The location's rates (tracked), or null when none were ever saved.</summary>
    Task<LocationCostRates?> GetByLocationAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken);

    void Add(LocationCostRates rates);
}
