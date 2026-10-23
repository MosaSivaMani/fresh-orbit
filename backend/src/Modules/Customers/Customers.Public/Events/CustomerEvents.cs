using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Customers.Public.Events;

public sealed record CustomerRegistered(Guid CustomerId, Guid UserId) : DomainEvent;

public sealed record CustomerDeleted(Guid CustomerId, Guid UserId, DateTimeOffset DeletedAt) : DomainEvent;

public sealed record CustomerBlocked(Guid CustomerId, Guid UserId) : DomainEvent;

public sealed record CustomerUnblocked(Guid CustomerId, Guid UserId) : DomainEvent;
