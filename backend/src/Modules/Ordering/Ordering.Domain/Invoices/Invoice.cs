using System.Globalization;
using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Pricing;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Ordering.Domain.Invoices;

/// <summary>
/// Seller facts frozen onto an invoice when it is issued (D342): legal name, registered address and GSTIN from the
/// Settings business profile; the FSSAI licence of the fulfillment location that supplied the order (Locations), because
/// an FSSAI licence belongs to the premises.
/// </summary>
public sealed record InvoiceSeller(string LegalName, string Gstin, string Address, string FssaiLicenseNo);

/// <summary>
/// A frozen GST invoice issued at delivery from the order's final amounts (<c>ordering.invoices</c>, ORD-R48..R52).
/// Never edited or deleted.
/// </summary>
public sealed class Invoice : AggregateRoot
{
    private readonly List<InvoiceLine> _lines = [];

    private Invoice()
    {
        InvoiceNumber = SellerGstin = SellerLegalName = string.Empty;
    }

    private Invoice(Order order, string invoiceNumber, InvoiceSeller seller, DateTimeOffset issuedAt)
    {
        OrderId = order.Id;
        InvoiceNumber = invoiceNumber;
        SellerGstin = seller.Gstin;
        SellerLegalName = seller.LegalName;
        SellerAddress = seller.Address;
        SellerFssaiLicenseNo = seller.FssaiLicenseNo;
        IssuedAt = issuedAt;
    }

    public Guid OrderId { get; private set; }

    public string InvoiceNumber { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public string SellerGstin { get; private set; }

    public string SellerLegalName { get; private set; }

    /// <summary>The seller's registered address at issue (D342); null only on invoices issued before it was frozen.</summary>
    public string? SellerAddress { get; private set; }

    /// <summary>FSSAI licence of the supplying premises at issue (D342); null only on invoices issued before it was frozen.</summary>
    public string? SellerFssaiLicenseNo { get; private set; }

    public long TaxablePaise { get; private set; }

    public long TaxPaise { get; private set; }

    public long TotalPaise { get; private set; }

    public string? PdfStorageKey { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    /// <summary>Media type of the rendered invoice (ORD-R53).</summary>
    public const string PdfContentType = "application/pdf";

    /// <summary>
    /// Storage key of the rendered PDF (D343). The <c>.pdf</c> extension is never served by <c>/media</c>, so the file is
    /// read only through the authenticated invoice endpoints.
    /// </summary>
    public static string PdfKey(Guid invoiceId) => $"invoices/{invoiceId:D}.pdf";

    /// <summary>
    /// Indian financial-year period <c>yy-yy</c> of <paramref name="at"/> in <paramref name="timeZone"/> (April–March).
    /// </summary>
    public static string FinancialYearPeriod(DateTimeOffset at, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var local = TimeZoneInfo.ConvertTime(at, timeZone);
        var startYear = local.Month >= 4 ? local.Year : local.Year - 1;
        return string.Create(CultureInfo.InvariantCulture, $"{startYear % 100:00}-{(startYear + 1) % 100:00}");
    }

    /// <summary>
    /// Builds the invoice (ORD-R49, R50): one line per charged item, total = final line − final discount, tax contained
    /// at the snapshot rate, plus a delivery-fee line; Σ lines must equal the order's final total.
    /// </summary>
    public static Invoice Issue(Order order, string invoiceNumber, InvoiceSeller seller, string deliveryServiceCode, DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(seller);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(seller.LegalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(seller.Gstin);
        ArgumentException.ThrowIfNullOrWhiteSpace(seller.Address);
        ArgumentException.ThrowIfNullOrWhiteSpace(seller.FssaiLicenseNo);
        if (order.FinalTotalPaise is not { } finalTotal)
        {
            throw new InvalidOperationException("An invoice needs the order's final amounts (packed order).");
        }

        var invoice = new Invoice(order, invoiceNumber, seller, issuedAt);
        var lineNo = 0;
        foreach (var item in order.Items.Where(i => i.Status == OrderItemStatus.Active && i.ChargedQuantity > 0).OrderBy(i => i.LineNo))
        {
            var total = (item.FinalTotalPaise ?? 0) - (item.FinalDiscountPaise ?? 0);
            var unitPrice = item.UnitPricePaise;
            invoice._lines.Add(new InvoiceLine(invoice.Id, ++lineNo, item.Description, item.HsnCode, item.ChargedQuantity!.Value, item.QuantityUnit, unitPrice, item.TaxRateBp, total));
        }

        if (order.DeliveryFeePaise > 0)
        {
            invoice._lines.Add(new InvoiceLine(invoice.Id, ++lineNo, "Delivery charge", deliveryServiceCode, 1, OrderUnits.Piece, order.DeliveryFeePaise, order.DeliveryFeeTaxRateBp, order.DeliveryFeePaise));
        }

        invoice.TotalPaise = Money.Sum(invoice._lines.Select(l => l.TotalPaise));
        invoice.TaxPaise = Money.Sum(invoice._lines.Select(l => l.TaxPaise));
        invoice.TaxablePaise = invoice.TotalPaise - invoice.TaxPaise;
        if (invoice.TotalPaise != finalTotal)
        {
            throw new InvalidOperationException($"Invoice lines total {invoice.TotalPaise} paise but the order's final total is {finalTotal} paise.");
        }

        invoice.Raise(new InvoiceIssued(order.Id, order.OrderNumber, order.CustomerId, order.FulfillmentLocationId, invoice.Id, invoiceNumber, issuedAt, invoice.TaxablePaise, invoice.TaxPaise, invoice.TotalPaise));
        return invoice;
    }

    /// <summary>Records the rendered PDF's storage key (ORD-R53); the invoice's amounts never change.</summary>
    public void SetPdf(string storageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        PdfStorageKey = storageKey;
    }
}

/// <summary>One frozen invoice line (<c>ordering.invoice_lines</c>).</summary>
public sealed class InvoiceLine : Entity
{
    private InvoiceLine()
    {
        Description = HsnCode = Unit = string.Empty;
    }

    internal InvoiceLine(Guid invoiceId, int lineNo, string description, string hsnCode, int quantity, string unit, long unitPricePaise, int taxRateBp, long totalPaise)
    {
        InvoiceId = invoiceId;
        LineNo = lineNo;
        Description = description;
        HsnCode = hsnCode;
        Quantity = quantity;
        Unit = unit;
        UnitPricePaise = unitPricePaise;
        TaxRateBp = taxRateBp;
        TotalPaise = totalPaise;
        TaxPaise = LinePricing.ContainedTax(totalPaise, taxRateBp);
        TaxablePaise = totalPaise - TaxPaise;
        CgstPaise = TaxPaise / 2;
        SgstPaise = TaxPaise - CgstPaise;
    }

    public Guid InvoiceId { get; private set; }

    public int LineNo { get; private set; }

    public string Description { get; private set; }

    public string HsnCode { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>GRAM / PIECE / PACK (G10).</summary>
    public string Unit { get; private set; }

    public long UnitPricePaise { get; private set; }

    public long TaxablePaise { get; private set; }

    public int TaxRateBp { get; private set; }

    public long TaxPaise { get; private set; }

    /// <summary>Half of the contained tax (intra-state supply, Q15); the odd paisa goes to SGST.</summary>
    public long CgstPaise { get; private set; }

    public long SgstPaise { get; private set; }

    public long TotalPaise { get; private set; }
}
