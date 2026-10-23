using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Inventory.Domain.ColdChain.Events;

/// <summary>A temperature outside the storage unit's range was recorded (cold-chain entry or receipt).</summary>
public sealed record ColdChainBreachRecorded(
    Guid ColdChainLogId,
    Guid FulfillmentLocationId,
    string StorageUnit,
    decimal TemperatureC,
    DateTimeOffset RecordedAt) : DomainEvent;
