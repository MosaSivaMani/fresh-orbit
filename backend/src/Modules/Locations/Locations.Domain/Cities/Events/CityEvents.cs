using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Locations.Domain.Cities.Events;

public sealed record CityActivated(Guid CityId, string Code, DateTimeOffset ActivatedAt, Guid? ActivatedBy) : DomainEvent;

public sealed record CityPaused(Guid CityId) : DomainEvent;

public sealed record CityRetired(Guid CityId) : DomainEvent;
