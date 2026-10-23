using FreshOrbit.Inventory.Domain.ColdChain.Events;
using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Inventory.Domain.ColdChain;

/// <summary>One temperature reading of a storage unit at a location (insert-only). Out-of-range is decided by the server (INV-R18).</summary>
public sealed class ColdChainLog : AggregateRoot
{
    private ColdChainLog()
    {
        StorageUnit = string.Empty;
    }

    private ColdChainLog(Guid fulfillmentLocationId, string storageUnit, decimal temperatureC, bool isOutOfRange, DateTimeOffset recordedAt, Guid? recordedBy)
    {
        FulfillmentLocationId = fulfillmentLocationId;
        StorageUnit = storageUnit;
        TemperatureC = temperatureC;
        IsOutOfRange = isOutOfRange;
        RecordedAt = recordedAt;
        RecordedBy = recordedBy;
    }

    public Guid FulfillmentLocationId { get; private set; }

    /// <summary>Code of a <see cref="StorageUnitRange"/> (reference data), e.g. CHILLER.</summary>
    public string StorageUnit { get; private set; }

    public decimal TemperatureC { get; private set; }

    public bool IsOutOfRange { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public Guid? RecordedBy { get; private set; }

    public static ColdChainLog Record(Guid fulfillmentLocationId, StorageUnitRange range, decimal temperatureC, DateTimeOffset recordedAt, Guid? recordedBy)
    {
        ArgumentNullException.ThrowIfNull(range);
        var log = new ColdChainLog(fulfillmentLocationId, range.Code, temperatureC, !range.Contains(temperatureC), recordedAt, recordedBy);
        if (log.IsOutOfRange)
        {
            log.Raise(new ColdChainBreachRecorded(log.Id, fulfillmentLocationId, range.Code, temperatureC, recordedAt));
        }

        return log;
    }
}
