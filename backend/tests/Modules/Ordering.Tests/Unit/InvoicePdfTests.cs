using System.Text;
using AwesomeAssertions;
using FreshOrbit.Ordering.Application.Invoices;
using FreshOrbit.Ordering.Domain.Invoices;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Infrastructure.Invoices;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Ordering.Tests.Unit;

/// <summary>Invoice seller snapshot (D342), PDF content rules (D344) and the SkiaSharp renderer (D345).</summary>
public sealed class InvoicePdfTests
{
    private static readonly DateTimeOffset DeliveredAt = new(2026, 10, 23, 19, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
    private static readonly InvoiceSeller Seller = new("Fresh Orbit Foods Pvt Ltd", "33ABCDE1234F1Z5", "1 Beach Road, Chennai 600041", "12426999000123");

    [Fact]
    public void Issue_freezes_the_seller_address_and_fssai_licence()
    {
        var invoice = Invoice.Issue(PackedOrder(), "INV/26-27/000001", Seller, "996813", DeliveredAt);

        invoice.SellerLegalName.Should().Be("Fresh Orbit Foods Pvt Ltd");
        invoice.SellerGstin.Should().Be("33ABCDE1234F1Z5");
        invoice.SellerAddress.Should().Be("1 Beach Road, Chennai 600041");
        invoice.SellerFssaiLicenseNo.Should().Be("12426999000123");
        invoice.PdfStorageKey.Should().BeNull();
    }

    [Fact]
    public void Issue_refuses_a_seller_without_an_fssai_licence()
    {
        var act = () => Invoice.Issue(PackedOrder(), "INV/26-27/000001", Seller with { FssaiLicenseNo = " " }, "996813", DeliveredAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Pdf_key_is_fixed_per_invoice_and_is_a_valid_private_storage_key()
    {
        var id = Guid.CreateVersion7();

        Invoice.PdfKey(id).Should().Be($"invoices/{id:D}.pdf");
        FreshOrbit.SharedKernel.Storage.StorageKeys.IsValid(Invoice.PdfKey(id)).Should().BeTrue();
    }

    [Theory]
    [InlineData(0L, "0.00")]
    [InlineData(5L, "0.05")]
    [InlineData(99900L, "999.00")]
    [InlineData(129900L, "1,299.00")]
    [InlineData(12345678L, "1,23,456.78")]
    [InlineData(1234567890L, "1,23,45,678.90")]
    [InlineData(-4900L, "-49.00")]
    public void Rupees_use_indian_digit_grouping(long paise, string expected)
        => IndianRupees.Format(paise).Should().Be(expected);

    [Theory]
    [InlineData(500, "5%")]
    [InlineData(250, "2.5%")]
    [InlineData(1800, "18%")]
    [InlineData(0, "0%")]
    public void Gst_rate_prints_as_a_percentage(int rateBp, string expected)
        => InvoicePdfContent.RateBpText(rateBp).Should().Be(expected);

    [Theory]
    [InlineData("TN", "Tamil Nadu", "33")]
    [InlineData("ap", "Andhra Pradesh", "37")]
    [InlineData("TS", "Telangana", "36")]
    [InlineData("TG", "Telangana", "36")]
    public void Address_state_maps_to_the_gst_state_code(string stateCode, string name, string code)
        => GstStates.Find(stateCode).Should().Be(new GstState(name, code));

    [Fact]
    public void Content_prints_the_local_invoice_date_place_of_supply_and_tax_split()
    {
        var order = PackedOrder();
        var invoice = Invoice.Issue(order, "INV/26-27/000001", Seller, "996813", DeliveredAt);

        var content = InvoicePdfContent.From(invoice, order, India);

        content.InvoiceDate.Should().Be(new DateOnly(2026, 10, 24), "19:00 UTC is 00:30 the next day in India");
        content.PlaceOfSupply.Should().Be(new GstState("Tamil Nadu", "33"));
        content.Seller.FssaiLicenseNo.Should().Be("12426999000123");
        content.Buyer.Name.Should().Be("Priya Raman");
        content.Buyer.AddressLines.Should().Contain("Tamil Nadu 600041");
        content.Lines.Should().HaveCount(invoice.Lines.Count);
        content.Lines[0].Quantity.Should().Be("0.540 kg");
        content.Lines[0].Rate.Should().Be("1,299.00/kg");
        (content.CgstPaise + content.SgstPaise).Should().Be(invoice.TaxPaise);
        content.IgstPaise.Should().Be(0);
        content.TotalPaise.Should().Be(invoice.TotalPaise);
    }

    [Fact]
    public void Content_refuses_an_inter_state_supply()
    {
        var order = PackedOrder();
        var invoice = Invoice.Issue(order, "INV/26-27/000001", Seller with { Gstin = "29ABCDE1234F1Z5" }, "996813", DeliveredAt);

        var act = () => InvoicePdfContent.From(invoice, order, India);

        var error = act.Should().Throw<DomainException>().Which;
        error.Code.Should().Be("PLACE_OF_SUPPLY_UNSUPPORTED");
        error.Field.Should().Be("stateCode");
    }

    [Fact]
    public void Embedded_fonts_print_the_rupee_sign()
    {
        using var renderer = new SkiaInvoicePdfRenderer();

        renderer.CanPrint("₹ 1,23,456.78 – Seer Fish").Should().BeTrue();
    }

    [Fact]
    public void Renderer_writes_a_pdf_and_continues_long_invoices_on_more_pages()
    {
        var order = PackedOrder();
        var invoice = Invoice.Issue(order, "INV/26-27/000001", Seller, "996813", DeliveredAt);
        var content = InvoicePdfContent.From(invoice, order, India);
        var many = content with
        {
            Lines = [.. Enumerable.Range(1, 90).Select(i => content.Lines[0] with { LineNo = i, Description = $"Seer Fish – Medium – Steaks, cleaned and cut into curry pieces, line {i}" })],
        };
        using var renderer = new SkiaInvoicePdfRenderer();

        var single = renderer.Render(content);
        var paged = renderer.Render(many);

        Encoding.ASCII.GetString(single, 0, 5).Should().Be("%PDF-");
        Encoding.ASCII.GetString(paged, 0, 5).Should().Be("%PDF-");
        PageCount(single).Should().Be(1);
        PageCount(paged).Should().BeGreaterThan(1);
    }

    private static int PageCount(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var count = 0;
        var at = 0;
        while ((at = text.IndexOf("/Type /Page", at, StringComparison.Ordinal)) >= 0)
        {
            at += "/Type /Page".Length;
            if (at < text.Length && text[at] != 's')
            {
                count++;
            }
        }

        return count;
    }

    private static Order PackedOrder()
    {
        var order = OrderFactory.Place([OrderFactory.SeerFish(), OrderFactory.Crabs()], deliveryFeePaise: 4900, deliveryFeeTaxRateBp: 1800).Preparing();
        order.Pack([new PackLine(order.Items[0].Id, 540, "GRAM"), new PackLine(order.Items[1].Id, 2, "PIECE")], false, 0, OrderFactory.Staff, DeliveredAt.AddHours(-2));
        return order;
    }
}
