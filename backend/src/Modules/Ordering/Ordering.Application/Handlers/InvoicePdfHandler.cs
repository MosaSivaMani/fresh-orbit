using FreshOrbit.Ordering.Application.Invoices;
using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Ordering.Application.Handlers;

/// <summary>
/// Follow-on of Ordering's own <see cref="InvoiceIssued"/> (ORD-R53, D343): renders the GST invoice PDF, stores it and
/// sets the invoice's PDF key. A failure retries with the outbox backoff and is then dead-lettered; delivery and the
/// invoice itself are already committed, so nothing else waits on it.
/// </summary>
public sealed class OrderingInvoicePdfHandler(InvoicePdfService pdfs) : IEventHandler<InvoiceIssued>
{
    public Task HandleAsync(InvoiceIssued domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return pdfs.RenderAsync(domainEvent.InvoiceId, cancellationToken);
    }
}
