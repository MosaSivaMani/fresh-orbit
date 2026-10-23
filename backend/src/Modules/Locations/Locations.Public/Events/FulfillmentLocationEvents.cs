using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Locations.Public.Events;

// Decision D30: events other modules consume (Delivery, Ordering, Pricing…) are part of the Locations public contract.

/// <param name="From">Previous status code (DRAFT / ACTIVE / PAUSED).</param>
/// <param name="To">New status code.</param>
public sealed record FulfillmentLocationStatusChanged(Guid FulfillmentLocationId, string From, string To) : DomainEvent;

public sealed record OperatingHoursChanged(Guid FulfillmentLocationId) : DomainEvent;

public sealed record LocationClosureAdded(Guid FulfillmentLocationId, DateOnly ClosedOn) : DomainEvent;

public sealed record LocationClosureRemoved(Guid FulfillmentLocationId, DateOnly ClosedOn) : DomainEvent;
