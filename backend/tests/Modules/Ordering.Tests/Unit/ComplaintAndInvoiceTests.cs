using AwesomeAssertions;
using FreshOrbit.Ordering.Domain.Complaints;
using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.Ordering.Domain.Invoices;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Ordering.Tests.Unit;

public sealed class ComplaintAndInvoiceTests
{
    private static readonly DateTimeOffset DeliveredAt = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private static ComplaintOrderFacts Facts => new(Guid.NewGuid(), "FO-260001", Guid.NewGuid(), Guid.NewGuid(), DeliveredAt);

    [Fact]
    public void Complaint_window_closes_after_the_configured_hours()
    {
        var facts = Facts;
        var inWindow = Complaint.Raise(facts, Guid.NewGuid(), ComplaintKind.Damaged, "The crab arrived crushed.", 24, false, DeliveredAt.AddHours(24));
        inWindow.Status.Should().Be(ComplaintStatus.Open);
        inWindow.DomainEvents.OfType<ComplaintRaised>().Should().ContainSingle();

        var late = () => Complaint.Raise(facts, Guid.NewGuid(), ComplaintKind.Damaged, "The crab arrived crushed.", 24, false, DeliveredAt.AddHours(24).AddSeconds(1));
        late.Should().Throw<DomainException>().Which.Code.Should().Be("COMPLAINT_WINDOW_CLOSED");
    }

    [Fact]
    public void Second_open_complaint_on_a_line_is_refused()
    {
        var act = () => Complaint.Raise(Facts, Guid.NewGuid(), ComplaintKind.Quality, "Fish was not fresh at all.", 24, true, DeliveredAt.AddHours(1));

        act.Should().Throw<ConflictException>().Which.Code.Should().Be("COMPLAINT_ALREADY_OPEN");
    }

    [Fact]
    public void Line_complaint_needs_a_line_and_a_real_description()
    {
        var act = () => Complaint.Raise(Facts, null, ComplaintKind.Missing, "short", 24, false, DeliveredAt.AddHours(1));

        act.Should().Throw<RefusalException>().Which.Errors.Select(e => e.Field).Should().BeEquivalentTo(["description", "orderItemId"]);
        Complaint.Raise(Facts, null, ComplaintKind.Late, "Arrived two hours late.", 24, false, DeliveredAt.AddHours(1)).OrderItemId.Should().BeNull();
    }

    [Fact]
    public void Decide_refund_is_bounded_and_resolve_needs_approval()
    {
        var complaint = Complaint.Raise(Facts, Guid.NewGuid(), ComplaintKind.Damaged, "The crab arrived crushed.", 24, false, DeliveredAt.AddHours(1));

        var tooMuch = () => complaint.Decide(ComplaintStatus.Approved, ComplaintResolution.Refund, 70000, 65000, null, Guid.NewGuid(), DeliveredAt.AddHours(2));
        tooMuch.Should().Throw<DomainException>().Which.Code.Should().Be("REFUND_AMOUNT_INVALID");

        complaint.Decide(ComplaintStatus.Approved, ComplaintResolution.Refund, 65000, 65000, "Photo confirms", Guid.NewGuid(), DeliveredAt.AddHours(2));
        complaint.DomainEvents.OfType<ComplaintDecided>().Should().ContainSingle().Which.RefundAmountPaise.Should().Be(65000);
        complaint.Resolve(Guid.NewGuid(), DeliveredAt.AddHours(3));
        complaint.Status.Should().Be(ComplaintStatus.Resolved);

        var rejected = Complaint.Raise(Facts, Guid.NewGuid(), ComplaintKind.Quality, "Not as described at all.", 24, false, DeliveredAt.AddHours(1));
        rejected.Decide(ComplaintStatus.Rejected, ComplaintResolution.Refund, 100, 1000, null, null, DeliveredAt.AddHours(2));
        rejected.Resolution.Should().Be(ComplaintResolution.None);
        var resolveRejected = () => rejected.Resolve(null, DeliveredAt.AddHours(3));
        resolveRejected.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
    }

    [Fact]
    public void Invoice_lines_sum_to_the_final_total_with_discount_and_delivery_line()
    {
        var parts = Money.Allocate(5000, [66950, 130000]);
        var order = OrderFactory.Place(
            [OrderFactory.Line("KG", 129900, 4000, "PER_KG", 500, true, 500, parts[0]), OrderFactory.Line("PIECE", 65000, 0, null, 2, false, 1200, parts[1])],
            discountPaise: 5000,
            deliveryFeePaise: 4900,
            deliveryFeeTaxRateBp: 1800);
        order.Preparing();
        order.Pack([new PackLine(order.Items[0].Id, 540, "GRAM"), new PackLine(order.Items[1].Id, 2, "PIECE")], false, 0, OrderFactory.Staff, DeliveredAt.AddHours(-2));

        var invoice = Invoice.Issue(order, "INV/26-27/000001", new InvoiceSeller("Fresh Orbit Foods Pvt Ltd", "33ABCDE1234F1Z5"), "996813", DeliveredAt);

        invoice.Lines.Should().HaveCount(3);
        invoice.TotalPaise.Should().Be(order.FinalTotalPaise);
        invoice.Lines[^1].TotalPaise.Should().Be(4900);
        invoice.Lines.Should().OnlyContain(l => l.TaxablePaise + l.TaxPaise == l.TotalPaise && l.CgstPaise + l.SgstPaise == l.TaxPaise);
        (invoice.TaxablePaise + invoice.TaxPaise).Should().Be(invoice.TotalPaise);
        invoice.DomainEvents.OfType<InvoiceIssued>().Should().ContainSingle().Which.InvoiceNumber.Should().Be("INV/26-27/000001");
    }

    [Fact]
    public void Short_supplied_lines_are_not_invoiced()
    {
        var order = OrderFactory.Place([OrderFactory.SeerFish(), OrderFactory.Crabs()]).Preparing();
        order.Pack([new PackLine(order.Items[0].Id, 0, "GRAM"), new PackLine(order.Items[1].Id, 2, "PIECE")], false, 0, OrderFactory.Staff, DeliveredAt);

        var invoice = Invoice.Issue(order, "INV/26-27/000002", new InvoiceSeller("Seller", "33ABCDE1234F1Z5"), "996813", DeliveredAt);

        invoice.Lines.Should().ContainSingle().Which.Quantity.Should().Be(2);
        invoice.TotalPaise.Should().Be(130000);
    }

    [Theory]
    [InlineData("2026-03-31T18:29:59Z", "25-26")]
    [InlineData("2026-03-31T18:30:00Z", "26-27")]
    [InlineData("2027-01-15T00:00:00Z", "26-27")]
    public void Invoice_period_is_the_indian_financial_year_in_the_business_time_zone(string at, string period)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        Invoice.FinancialYearPeriod(DateTimeOffset.Parse(at, System.Globalization.CultureInfo.InvariantCulture), zone).Should().Be(period);
    }
}
