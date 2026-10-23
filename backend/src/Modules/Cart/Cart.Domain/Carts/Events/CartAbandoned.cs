using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Cart.Domain.Carts.Events;

/// <summary>An ACTIVE cart passed its expiry and was abandoned by the AbandonCarts job.</summary>
public sealed record CartAbandoned(
    Guid CartId,
    Guid? CustomerId,
    int LineCount,
    string? Pincode,
    Guid? FulfillmentLocationId,
    DateTimeOffset LastActivityAt,
    DateTimeOffset AbandonedAt) : DomainEvent;
