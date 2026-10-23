using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Locations.Domain.ServiceAreas.Events;

public sealed record ServiceAreaChanged(Guid ServiceAreaId, long DeliveryFeePaise, long? FreeDeliveryAbovePaise, long MinOrderPaise, bool IsActive) : DomainEvent;

public sealed record PincodesChanged(Guid ServiceAreaId, IReadOnlyList<string> Added, IReadOnlyList<string> Removed) : DomainEvent;
