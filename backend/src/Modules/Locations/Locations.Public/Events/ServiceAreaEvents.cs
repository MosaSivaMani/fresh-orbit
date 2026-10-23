using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Locations.Public.Events;

// Decision D30: events other modules consume (Delivery, Ordering, Pricing…) are part of the Locations public contract.

public sealed record ServiceAreaChanged(Guid ServiceAreaId, long DeliveryFeePaise, long? FreeDeliveryAbovePaise, long MinOrderPaise, bool IsActive) : DomainEvent;

public sealed record PincodesChanged(Guid ServiceAreaId, IReadOnlyList<string> Added, IReadOnlyList<string> Removed) : DomainEvent;
