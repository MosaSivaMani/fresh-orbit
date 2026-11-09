namespace FreshOrbit.Ordering.Application.Invoices;

/// <summary>
/// One row of the staff invoice register (<c>GET /admin/invoices</c>, D353). Amounts are the frozen invoice's;
/// <c>InvoiceDate</c> is the local date of <c>IssuedAt</c> in the business time zone, the date printed on the PDF
/// (ORD-R53). <c>CustomerName</c> is the buyer printed on the invoice (the order's recipient). <c>PdfUrl</c> is the
/// authenticated staff download path once the PDF is rendered (D343), else null and <c>PdfReady</c> is false.
/// </summary>
public sealed record AdminInvoiceListItem(
    Guid Id,
    string InvoiceNumber,
    DateTimeOffset IssuedAt,
    DateOnly InvoiceDate,
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    Guid FulfillmentLocationId,
    long TaxablePaise,
    long TaxPaise,
    long TotalPaise,
    bool PdfReady,
    string? PdfUrl);

/// <param name="FromDate">First invoice date (business time zone), inclusive.</param>
/// <param name="ToDate">Last invoice date (business time zone), inclusive.</param>
/// <param name="Q">Part of the invoice number or the order number.</param>
public sealed record AdminInvoiceListQuery(
    Guid? FulfillmentLocationId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    string? Q,
    int Page,
    int PageSize);

/// <summary>
/// Typed register filter: <c>IssuedFrom</c> inclusive and <c>IssuedBefore</c> exclusive UTC instants;
/// <c>LocationIds</c> = locations the staff member may see (null = all).
/// </summary>
public sealed record AdminInvoiceFilter(
    Guid? FulfillmentLocationId,
    DateTimeOffset? IssuedFrom,
    DateTimeOffset? IssuedBefore,
    string? Q,
    IReadOnlySet<Guid>? LocationIds,
    int Page,
    int PageSize);

/// <summary>An invoice joined to its order, as read from the tables.</summary>
public sealed record AdminInvoiceRow(
    Guid Id,
    string InvoiceNumber,
    DateTimeOffset IssuedAt,
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    Guid FulfillmentLocationId,
    long TaxablePaise,
    long TaxPaise,
    long TotalPaise,
    bool PdfReady);
