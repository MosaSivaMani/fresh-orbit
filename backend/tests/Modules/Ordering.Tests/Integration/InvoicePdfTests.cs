using System.Net;
using System.Text;
using AwesomeAssertions;
using FreshOrbit.Locations.Public;
using FreshOrbit.Ordering.Application.Events;
using FreshOrbit.Ordering.Application.Invoices;
using FreshOrbit.Ordering.Domain.Invoices;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FreshOrbit.Ordering.Tests.Integration;

/// <summary>
/// L-12: the seller snapshot at delivery (D342), the <c>InvoiceIssued</c> PDF handler (D343, D345) and the customer and
/// staff download endpoints, against real Postgres and local-disk storage.
/// </summary>
public sealed class InvoicePdfTests(OrderingDatabase database) : OrderingTestBase(database)
{
    private static readonly DateTimeOffset DeliveredAt = Start.AddHours(6);

    [Fact]
    public async Task Delivery_freezes_the_seller_address_and_the_location_fssai_licence_on_the_invoice()
    {
        var orderId = await SeedOutForDeliveryAsync(CustomerId);

        (await DeliverAsync(orderId)).Should().Be(DeliveryEventOutcome.Applied);

        var invoice = await LoadInvoiceAsync(orderId);
        invoice.SellerLegalName.Should().Be(World.Profile.LegalName);
        invoice.SellerGstin.Should().Be(World.Profile.Gstin);
        invoice.SellerAddress.Should().Be(World.Profile.Address);
        invoice.SellerFssaiLicenseNo.Should().Be(CheckoutWorld.LocationFssai);
    }

    [Fact]
    public async Task Delivery_is_refused_while_the_location_has_no_fssai_licence()
    {
        var orderId = await SeedOutForDeliveryAsync(CustomerId);
        World.Locations.GetFulfillmentLocationsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new FulfillmentLocationSummary(CheckoutWorld.LocationId, CheckoutWorld.CityId, "CHN-TVM", "Thiruvanmiyur", FulfillmentLocationStatus.Active, CheckoutWorld.TimeZone)]);

        var act = () => DeliverAsync(orderId);

        var error = (await act.Should().ThrowAsync<DomainException>()).Which;
        error.Code.Should().Be("CONFIGURATION_MISSING");
        error.Field.Should().Be("fssaiLicenseNo");
        await using var db = Database.CreateDbContext();
        (await db.Set<Invoice>().AnyAsync(i => i.OrderId == orderId, Ct)).Should().BeFalse();
        (await LoadOrderAsync(orderId))!.Status.Should().Be(OrderStatus.OutForDelivery);
    }

    [Fact]
    public async Task Invoice_issued_handler_renders_and_stores_the_pdf_once()
    {
        var orderId = await SeedOutForDeliveryAsync(CustomerId);
        await DeliverAsync(orderId);
        var invoiceId = (await LoadInvoiceAsync(orderId)).Id;

        (await RenderAsync(invoiceId)).Should().BeTrue();
        (await RenderAsync(invoiceId)).Should().BeFalse("a replayed InvoiceIssued leaves the stored PDF alone");

        var invoice = await LoadInvoiceAsync(orderId);
        invoice.PdfStorageKey.Should().Be(Invoice.PdfKey(invoiceId));
        var storage = Factory.Services.GetRequiredService<IFileStorage>();
        await using var file = await storage.OpenReadAsync(invoice.PdfStorageKey!, Ct);
        file.Should().NotBeNull();
        var head = new byte[5];
        await file!.ReadExactlyAsync(head, Ct);
        Encoding.ASCII.GetString(head).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Customer_downloads_the_pdf_of_their_own_order_once_it_is_rendered()
    {
        var orderId = await SeedOutForDeliveryAsync(CustomerId);
        await DeliverAsync(orderId);

        var before = await ReadJsonAsync(await Customer.GetAsync(new Uri($"/api/v1/orders/{orderId}/invoice", UriKind.Relative), Ct));
        before.GetProperty("pdfUrl").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        before.GetProperty("sellerFssaiLicenseNo").GetString().Should().Be(CheckoutWorld.LocationFssai);
        var early = await Customer.GetAsync(new Uri($"/api/v1/orders/{orderId}/invoice/pdf", UriKind.Relative), Ct);
        early.StatusCode.Should().Be(HttpStatusCode.NotFound);
        CodeOf(await ReadJsonAsync(early)).Should().Be("INVOICE_PDF_NOT_READY");

        await RenderAsync((await LoadInvoiceAsync(orderId)).Id);

        var invoice = await ReadJsonAsync(await Customer.GetAsync(new Uri($"/api/v1/orders/{orderId}/invoice", UriKind.Relative), Ct));
        var pdfUrl = invoice.GetProperty("pdfUrl").GetString();
        pdfUrl.Should().Be($"/api/v1/orders/{orderId:D}/invoice/pdf");
        var pdf = await Customer.GetAsync(new Uri(pdfUrl!, UriKind.Relative), Ct);
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        pdf.Content.Headers.ContentDisposition!.FileName.Should().Contain("INV-26-27-");
        pdf.Headers.CacheControl!.Private.Should().BeTrue();
        Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync(Ct), 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Another_customer_gets_404_and_an_undelivered_order_has_no_invoice()
    {
        var orderId = await SeedOutForDeliveryAsync(CustomerId);
        await DeliverAsync(orderId);
        await RenderAsync((await LoadInvoiceAsync(orderId)).Id);
        using var stranger = ClientFor(f => f.CreateCustomerClient(customerId: Guid.CreateVersion7()));

        var other = await stranger.GetAsync(new Uri($"/api/v1/orders/{orderId}/invoice/pdf", UriKind.Relative), Ct);
        other.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var undelivered = await SeedOutForDeliveryAsync(CustomerId);
        var missing = await Customer.GetAsync(new Uri($"/api/v1/orders/{undelivered}/invoice/pdf", UriKind.Relative), Ct);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        CodeOf(await ReadJsonAsync(missing)).Should().Be("INVOICE_NOT_ISSUED");
    }

    [Fact]
    public async Task Staff_download_needs_orders_view_and_the_order_location()
    {
        var orderId = await SeedOutForDeliveryAsync(CustomerId);
        await DeliverAsync(orderId);
        await RenderAsync((await LoadInvoiceAsync(orderId)).Id);
        using var staff = Staff();

        var invoice = await ReadJsonAsync(await staff.GetAsync(new Uri($"/api/v1/admin/orders/{orderId}/invoice", UriKind.Relative), Ct));
        invoice.GetProperty("pdfUrl").GetString().Should().Be($"/api/v1/admin/orders/{orderId:D}/invoice/pdf");
        var pdf = await staff.GetAsync(new Uri($"/api/v1/admin/orders/{orderId}/invoice/pdf", UriKind.Relative), Ct);
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        using var packer = Staff("orders.pack");
        (await packer.GetAsync(new Uri($"/api/v1/admin/orders/{orderId}/invoice/pdf", UriKind.Relative), Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> SeedOutForDeliveryAsync(Guid customerId)
    {
        await using var db = Database.CreateDbContext();
        var order = OrderFactory.Place([OrderFactory.SeerFish(), OrderFactory.Crabs()], customerId: customerId, fulfillmentLocationId: CheckoutWorld.LocationId, placedAt: Start).Preparing().PackedAsOrdered();
        order.Dispatch(Guid.NewGuid(), null, Start.AddHours(4));
        db.Add(order);
        await db.SaveChangesAsync(Ct);
        return order.Id;
    }

    private Task<DeliveryEventOutcome> DeliverAsync(Guid orderId)
        => AsHandlerAsync(s => s.GetRequiredService<OrderEventService>().OnShipmentDeliveredAsync(orderId, Guid.NewGuid(), DeliveredAt, Ct));

    private Task<bool> RenderAsync(Guid invoiceId)
        => AsHandlerAsync(s => s.GetRequiredService<InvoicePdfService>().RenderAsync(invoiceId, Ct));

    private async Task<Invoice> LoadInvoiceAsync(Guid orderId)
    {
        await using var db = Database.CreateDbContext();
        return await db.Set<Invoice>().AsNoTracking().SingleAsync(i => i.OrderId == orderId, Ct);
    }
}
