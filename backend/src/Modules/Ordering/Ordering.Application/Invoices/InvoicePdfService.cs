using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.Ordering.Application.Orders;
using FreshOrbit.Ordering.Domain.Invoices;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Storage;

namespace FreshOrbit.Ordering.Application.Invoices;

/// <summary>Lays out <see cref="InvoicePdfContent"/> as PDF bytes (D345: SkiaSharp, fonts embedded).</summary>
public interface IInvoicePdfRenderer
{
    byte[] Render(InvoicePdfContent content);
}

/// <summary>A rendered invoice for download: the bytes and the file name to save it as.</summary>
public sealed record InvoicePdfFile(Stream Content, string FileName);

/// <summary>
/// The invoice PDF (ORD-R53, D343): rendered once by the <c>InvoiceIssued</c> follow-on handler and stored through
/// <see cref="IFileStorage"/>, then read back only through the authenticated customer and staff endpoints.
/// </summary>
public sealed class InvoicePdfService(
    IInvoiceRepository invoices,
    IOrderRepository orders,
    ISettingsApi settings,
    IInvoicePdfRenderer renderer,
    IFileStorage storage,
    OrderQueryService queries)
{
    /// <summary>
    /// Renders and stores the PDF of <paramref name="invoiceId"/> and records its key on the tracked invoice (the
    /// outbox dispatcher saves it). An invoice that already has its PDF is left alone (a replayed event). The key is
    /// fixed per invoice, so a retry after a failed save overwrites the same file. Returns true when it rendered.
    /// </summary>
    public async Task<bool> RenderAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetAsync(invoiceId, cancellationToken)
            ?? throw new NotFoundException($"Invoice {invoiceId} does not exist.");
        if (invoice.PdfStorageKey is not null)
        {
            return false;
        }

        var order = await orders.GetAsync(invoice.OrderId, cancellationToken)
            ?? throw new NotFoundException($"Order {invoice.OrderId} of invoice {invoice.InvoiceNumber} does not exist.");
        var profile = await settings.GetBusinessProfileAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(profile.TimeZone) || !TimeZoneInfo.TryFindSystemTimeZoneById(profile.TimeZone, out var zone))
        {
            throw new DomainException(OrderingErrors.ConfigurationMissing, "The business profile timeZone is not a known time zone; the invoice date cannot be printed.", "timeZone");
        }

        var bytes = renderer.Render(InvoicePdfContent.From(invoice, order, zone));
        var key = Invoice.PdfKey(invoice.Id);
        using (var content = new MemoryStream(bytes, writable: false))
        {
            await storage.SaveAsync(key, content, Invoice.PdfContentType, cancellationToken);
        }

        invoice.SetPdf(key);
        return true;
    }

    /// <summary>The PDF of the customer's own delivered order (404 for anyone else's).</summary>
    public async Task<InvoicePdfFile> OpenForCustomerAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
        => await OpenAsync(await queries.GetOwnedAsync(customerId, orderId, cancellationToken), cancellationToken);

    /// <summary>The PDF of an order at one of the staff member's locations (404 / 403).</summary>
    public async Task<InvoicePdfFile> OpenForStaffAsync(ICurrentActor actor, Guid orderId, CancellationToken cancellationToken)
        => await OpenAsync(await queries.GetForStaffAsync(actor, orderId, cancellationToken), cancellationToken);

    /// <summary>Customer download path of the PDF (D343), or null while it is not rendered.</summary>
    public static string? CustomerUrl(Invoice invoice)
        => invoice is { PdfStorageKey: not null } ? $"/api/v1/orders/{invoice.OrderId:D}/invoice/pdf" : null;

    /// <summary>Staff download path of the PDF (D343), or null while it is not rendered.</summary>
    public static string? StaffUrl(Invoice invoice)
        => invoice is { PdfStorageKey: not null } ? $"/api/v1/admin/orders/{invoice.OrderId:D}/invoice/pdf" : null;

    /// <summary>File name offered on download: the invoice number with <c>/</c> turned into <c>-</c>.</summary>
    public static string FileNameOf(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        return $"{invoice.InvoiceNumber.Replace('/', '-')}.pdf";
    }

    private async Task<InvoicePdfFile> OpenAsync(Order order, CancellationToken cancellationToken)
    {
        var invoice = await invoices.FindByOrderAsync(order.Id, cancellationToken)
            ?? throw new NotFoundException(OrderingErrors.InvoiceNotIssued, "The invoice is issued when the order is delivered.");
        var stream = invoice.PdfStorageKey is { } key ? await storage.OpenReadAsync(key, cancellationToken) : null;
        return stream is null
            ? throw new NotFoundException(OrderingErrors.InvoicePdfNotReady, "The invoice PDF is being prepared; try again in a minute.")
            : new InvoicePdfFile(stream, FileNameOf(invoice));
    }
}
