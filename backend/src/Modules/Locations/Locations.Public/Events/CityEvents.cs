using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Locations.Public.Events;

// Decision D30: events other modules consume (Delivery, Ordering, Pricing…) are part of the Locations public contract.

public sealed record CityActivated(Guid CityId, string Code, DateTimeOffset ActivatedAt, Guid? ActivatedBy) : DomainEvent;

public sealed record CityPaused(Guid CityId) : DomainEvent;

public sealed record CityRetired(Guid CityId) : DomainEvent;
