using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Ordering.Public.Events;

/// <summary>An invoice was issued at delivery (PDF rendering, email).</summary>
public sealed record InvoiceIssued(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid FulfillmentLocationId,
    Guid InvoiceId,
    string InvoiceNumber,
    DateTimeOffset IssuedAt,
    long TaxablePaise,
    long TaxPaise,
    long TotalPaise) : DomainEvent;
