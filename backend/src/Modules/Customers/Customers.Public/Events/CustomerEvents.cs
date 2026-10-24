using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Customers.Public.Events;

public sealed record CustomerRegistered(Guid CustomerId, Guid UserId) : DomainEvent;

public sealed record CustomerDeleted(Guid CustomerId, Guid UserId, DateTimeOffset DeletedAt) : DomainEvent;

public sealed record CustomerBlocked(Guid CustomerId, Guid UserId) : DomainEvent;

public sealed record CustomerUnblocked(Guid CustomerId, Guid UserId) : DomainEvent;

/// <summary>
/// A back-in-stock alert fired: the variant is available again at the location, and the alert was cleared in the same
/// save. Notifications tells the customer (template BACK_IN_STOCK).
/// </summary>
public sealed record StockAlertTriggered(
    Guid StockAlertId,
    Guid CustomerId,
    Guid VariantId,
    Guid FulfillmentLocationId,
    string ProductName,
    string ProductSlug,
    string VariantName) : DomainEvent;
