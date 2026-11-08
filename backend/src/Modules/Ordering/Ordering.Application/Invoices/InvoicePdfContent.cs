using System.Globalization;
using FreshOrbit.Ordering.Domain.Invoices;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Ordering.Application.Invoices;

/// <summary>The seller block of a GST invoice PDF (frozen on the invoice at issue, D342).</summary>
public sealed record InvoicePdfSeller(string LegalName, string Address, string Gstin, string FssaiLicenseNo);

/// <summary>The recipient block: the delivery name and address frozen on the order (ORD-R16).</summary>
public sealed record InvoicePdfBuyer(string Name, IReadOnlyList<string> AddressLines);

/// <summary>
/// One printed line. Amounts are paise; <see cref="Quantity"/> and <see cref="Rate"/> are display text built by the
/// server (rule 4: the renderer formats, it never computes).
/// </summary>
public sealed record InvoicePdfLine(
    int LineNo,
    string Description,
    string HsnCode,
    string Quantity,
    string Rate,
    long TaxablePaise,
    int TaxRateBp,
    long CgstPaise,
    long SgstPaise,
    long TotalPaise);

/// <summary>
/// Everything a GST invoice PDF prints (CGST Rules, rule 46; FSSAI licence on the bill), taken only from the frozen
/// invoice and order (ORD-R53). Built by <see cref="From"/>; the renderer lays it out and adds no figures of its own.
/// </summary>
public sealed record InvoicePdfContent(
    string InvoiceNumber,
    DateOnly InvoiceDate,
    string OrderNumber,
    InvoicePdfSeller Seller,
    InvoicePdfBuyer Buyer,
    GstState PlaceOfSupply,
    IReadOnlyList<InvoicePdfLine> Lines,
    long TaxablePaise,
    long CgstPaise,
    long SgstPaise,
    long IgstPaise,
    long TotalPaise)
{
    /// <summary>Document title. The CA has not yet confirmed tax invoice versus bill of supply for exempt fish (L-13).</summary>
    public const string Title = "Tax invoice";

    /// <summary>
    /// Builds the content of <paramref name="invoice"/> for <paramref name="order"/>; the invoice date is the issue
    /// instant's local date in <paramref name="businessTimeZone"/> (the zone that also numbers it, ORD-R51).
    /// Refuses (D344) an invoice without its seller snapshot, an address state outside the GST list, and an inter-state
    /// supply (IGST is not computed yet).
    /// </summary>
    public static InvoicePdfContent From(Invoice invoice, Order order, TimeZoneInfo businessTimeZone)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(businessTimeZone);
        if (string.IsNullOrWhiteSpace(invoice.SellerAddress) || string.IsNullOrWhiteSpace(invoice.SellerFssaiLicenseNo))
        {
            throw new DomainException(
                OrderingErrors.ConfigurationMissing,
                $"Invoice {invoice.InvoiceNumber} was issued without the seller's address or fssaiLicenseNo, so its PDF cannot be rendered.",
                string.IsNullOrWhiteSpace(invoice.SellerAddress) ? "sellerAddress" : "fssaiLicenseNo");
        }

        var placeOfSupply = GstStates.Find(order.StateCode)
            ?? throw new DomainException(OrderingErrors.PlaceOfSupplyUnsupported, $"The delivery address stateCode '{order.StateCode}' is not an Indian state or union territory.", "stateCode");
        if (GstStates.CodeOfGstin(invoice.SellerGstin) != placeOfSupply.Code)
        {
            throw new DomainException(
                OrderingErrors.PlaceOfSupplyUnsupported,
                $"The delivery stateCode '{order.StateCode}' is outside the seller GSTIN's state; inter-state supply (IGST) is not supported yet.",
                "stateCode");
        }

        var lines = invoice.Lines.OrderBy(l => l.LineNo)
            .Select(l => new InvoicePdfLine(l.LineNo, l.Description, l.HsnCode, QuantityText(l.Quantity, l.Unit), RateText(l.UnitPricePaise, l.Unit), l.TaxablePaise, l.TaxRateBp, l.CgstPaise, l.SgstPaise, l.TotalPaise))
            .ToList();
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(invoice.IssuedAt, businessTimeZone).DateTime);
        return new InvoicePdfContent(
            invoice.InvoiceNumber,
            date,
            order.OrderNumber,
            new InvoicePdfSeller(invoice.SellerLegalName, invoice.SellerAddress, invoice.SellerGstin, invoice.SellerFssaiLicenseNo),
            new InvoicePdfBuyer(order.RecipientName, BuyerAddress(order, placeOfSupply)),
            placeOfSupply,
            lines,
            invoice.TaxablePaise,
            lines.Sum(l => l.CgstPaise),
            lines.Sum(l => l.SgstPaise),
            0,
            invoice.TotalPaise);
    }

    /// <summary>A GRAM quantity in kilograms with three decimals; pieces and packs as counts.</summary>
    public static string QuantityText(int quantity, string unit) => unit switch
    {
        OrderUnits.Gram => string.Create(CultureInfo.InvariantCulture, $"{quantity / 1000m:0.000} kg"),
        OrderUnits.Pack => string.Create(CultureInfo.InvariantCulture, $"{quantity} pack"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{quantity} pc"),
    };

    /// <summary>The unit price per kilogram (GRAM lines) or per piece / pack, in rupees.</summary>
    public static string RateText(long unitPricePaise, string unit) => unit switch
    {
        OrderUnits.Gram => IndianRupees.Format(unitPricePaise) + "/kg",
        OrderUnits.Pack => IndianRupees.Format(unitPricePaise) + "/pack",
        _ => IndianRupees.Format(unitPricePaise) + "/pc",
    };

    /// <summary>A GST rate in basis points as a percentage: 500 → "5%", 250 → "2.5%".</summary>
    public static string RateBpText(int rateBp)
        => string.Create(CultureInfo.InvariantCulture, $"{rateBp / 100m:0.##}%");

    private static List<string> BuyerAddress(Order order, GstState state)
    {
        var lines = new List<string> { order.Line1 };
        if (!string.IsNullOrWhiteSpace(order.Line2))
        {
            lines.Add(order.Line2);
        }

        if (!string.IsNullOrWhiteSpace(order.Landmark))
        {
            lines.Add(order.Landmark);
        }

        lines.Add($"{order.Locality}, {order.CityName}");
        lines.Add($"{state.Name} {order.Pincode}");
        return lines;
    }
}

/// <summary>Rupee amounts from paise with Indian digit grouping (12,34,567.89); no currency sign.</summary>
public static class IndianRupees
{
    public static string Format(long paise)
    {
        var negative = paise < 0;
        var magnitude = negative ? -(decimal)paise : paise;
        var rupees = decimal.Truncate(magnitude / 100m);
        var fraction = (int)(magnitude - (rupees * 100m));
        var digits = rupees.ToString("0", CultureInfo.InvariantCulture);
        string grouped;
        if (digits.Length <= 3)
        {
            grouped = digits;
        }
        else
        {
            var head = digits[..^3];
            var groups = new List<string>();
            while (head.Length > 2)
            {
                groups.Insert(0, head[^2..]);
                head = head[..^2];
            }

            groups.Insert(0, head);
            grouped = string.Join(',', groups) + "," + digits[^3..];
        }

        return string.Create(CultureInfo.InvariantCulture, $"{(negative ? "-" : string.Empty)}{grouped}.{fraction:00}");
    }
}
