using System.Text;
using AwesomeAssertions;
using FreshOrbit.Payments.Domain.Events;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Settlements;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.Payments.Infrastructure.Providers;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Payments.Tests.Unit;

public sealed class PaymentDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 6, 0, 0, TimeSpan.Zero);
    private static readonly Guid Staff = Guid.CreateVersion7();
    private static readonly Guid OtherStaff = Guid.CreateVersion7();

    [Fact]
    public void U1_online_attempt_follows_the_status_table()
    {
        var payment = Online(66950);
        payment.Authorize("fake_pay_1", Now).Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Authorized);
        payment.Authorize("fake_pay_1", Now).Should().BeFalse();

        payment.Capture("fake_pay_1", Now, orderAlreadyPaid: false).Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Captured);
        payment.Capture("fake_pay_1", Now, orderAlreadyPaid: false).Should().BeFalse("capturing twice changes nothing");
        payment.Fail(null, "X", null, false).Should().BeFalse("CAPTURED is terminal");
        payment.Cancel(Now).Should().BeFalse();
        payment.DomainEvents.OfType<PaymentCaptured>().Should().ContainSingle();
    }

    [Fact]
    public void U1_late_capture_after_cancel_is_accepted_and_failed_attempt_cannot_be_cancelled()
    {
        var cancelled = Online(1000);
        cancelled.Cancel(Now).Should().BeTrue();
        cancelled.Capture("p", Now, orderAlreadyPaid: true).Should().BeTrue();
        cancelled.IsExcess.Should().BeTrue();

        var failed = Online(1000);
        failed.Fail("p2", "DECLINED", "Card declined", hasOtherActiveAttempt: true).Should().BeTrue();
        failed.Cancel(Now).Should().BeFalse();
        failed.DomainEvents.OfType<PaymentFailed>().Single().HasOtherActiveAttempt.Should().BeTrue();
    }

    [Fact]
    public void U1_delivery_payment_cannot_be_captured_and_collected_twice_is_a_conflict()
    {
        var cod = Payment.CreateForDelivery(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), PaymentMethod.Cod, 234900);
        var capture = () => cod.Capture("x", Now, false);
        capture.Should().Throw<ConflictException>().Which.Code.Should().Be(PaymentsErrors.InvalidStatusTransition);

        cod.RecordCollection(234900, CollectionMode.Cash, null, null, Now, requireExactAmount: true);
        var again = () => cod.RecordCollection(234900, CollectionMode.Cash, null, null, Now, requireExactAmount: true);
        again.Should().Throw<ConflictException>().Which.Code.Should().Be(PaymentsErrors.CashAlreadyCollected);
    }

    [Fact]
    public void U2_refund_amount_is_limited_by_paid_minus_refunds_holding_an_amount()
    {
        var payment = CollectedCod(245051);
        Approve(payment, Request(payment, 20000, RefundMethod.Cash));
        payment.MarkRefundPaid(payment.Refunds[0].Id, "RCPT-0001", OtherStaff, Now);
        Request(payment, 5000, RefundMethod.Cash);
        var failed = Request(payment, 9000, RefundMethod.Cash);
        payment.RejectRefund(failed.Id, OtherStaff, "Duplicate");

        var tooMuch = () => Request(payment, 220052, RefundMethod.Cash);
        tooMuch.Should().Throw<DomainException>().Which.Code.Should().Be(PaymentsErrors.RefundExceedsPayment);
        Request(payment, 220051, RefundMethod.Cash).AmountPaise.Should().Be(220051);
        payment.RefundablePaise.Should().Be(0);
        payment.RefundedPaise.Should().Be(20000);
        payment.RefundPendingPaise.Should().Be(225051);
    }

    [Fact]
    public void U3_requester_cannot_approve_own_refund()
    {
        var payment = CollectedCod(10000);
        var refund = Request(payment, 1000, RefundMethod.Cash);

        var approve = () => payment.ApproveRefund(refund.Id, Staff, Now);
        approve.Should().Throw<DomainException>().Which.Code.Should().Be(PaymentsErrors.RefundSelfApproval);
    }

    [Fact]
    public void U4_only_requested_refunds_are_approvable()
    {
        var payment = CollectedCod(10000);
        var refund = Request(payment, 1000, RefundMethod.Cash);
        payment.ApproveRefund(refund.Id, OtherStaff, Now);

        var again = () => payment.ApproveRefund(refund.Id, OtherStaff, Now);
        again.Should().Throw<ConflictException>().Which.Code.Should().Be(PaymentsErrors.RefundNotApprovable);
    }

    [Fact]
    public void U5_original_needs_provider_refunds_and_upi_needs_an_upi_id()
    {
        var payment = CollectedCod(10000);
        var original = () => payment.RequestRefund(new RefundDraft(1000, RefundReason.Goodwill, RefundMethod.Original, Staff, null, null, null, null), false, Now);
        original.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "method" && e.Code == PaymentsErrors.RefundMethodNotAllowed);

        var upi = () => payment.RequestRefund(new RefundDraft(1000, RefundReason.Goodwill, RefundMethod.Upi, Staff, null, null, null, null), false, Now);
        var refusal = upi.Should().Throw<RefusalException>().Which;
        refusal.Code.Should().Be(PaymentsErrors.ValidationFailed);
        refusal.Errors.Should().ContainSingle(e => e.Field == "upiId");
    }

    [Fact]
    public void U6_mark_paid_on_original_refund_is_an_invalid_transition()
    {
        var payment = Online(66950);
        payment.Capture("pay", Now, false);
        var result = payment.CreateSystemRefund(RefundReason.Cancellation, 66950, "OrderCancelled:x", Now);

        var markPaid = () => payment.MarkRefundPaid(result!.Refund!.Id, "UTR12345", Staff, Now);
        markPaid.Should().Throw<ConflictException>().Which.Code.Should().Be(PaymentsErrors.InvalidStatusTransition);
    }

    [Fact]
    public void System_refund_is_approved_capped_and_idempotent_by_source_reference()
    {
        var payment = Online(66950);
        payment.Capture("pay", Now, false);

        var first = payment.CreateSystemRefund(RefundReason.WeightShortfall, 6695, "OrderPacked:o:WEIGHT_SHORTFALL", Now)!;
        first.Refund!.Status.Should().Be(RefundStatus.Approved);
        first.Refund.RequestedBy.Should().BeNull();
        payment.CreateSystemRefund(RefundReason.WeightShortfall, 6695, "OrderPacked:o:WEIGHT_SHORTFALL", Now).Should().BeNull();

        var capped = payment.CreateSystemRefund(RefundReason.Cancellation, 100000, "OrderCancelled:o", Now)!;
        capped.WasCapped.Should().BeTrue();
        capped.Refund!.AmountPaise.Should().Be(66950 - 6695);
        payment.DomainEvents.OfType<RefundApproved>().Should().HaveCount(2);
    }

    [Fact]
    public void Every_refund_change_moves_the_refund_revision()
    {
        var payment = Online(1000);
        payment.Capture("pay", Now, false);
        var refund = payment.CreateSystemRefund(RefundReason.Cancellation, 1000, "c", Now)!.Refund!;
        var revision = payment.RefundRevision;

        payment.StartRefundProcessing(refund.Id).Should().BeTrue();
        payment.RefundRevision.Should().BeGreaterThan(revision);
        payment.StartRefundProcessing(refund.Id).Should().BeTrue("re-entry without a provider refund id calls the provider again");
        refund.AttemptCount.Should().Be(2);
        payment.ApplyProviderRefundResult(refund.Id, "fake_rfnd_1", ProviderRefundStatus.Processed, null, Now);
        refund.Status.Should().Be(RefundStatus.Succeeded);
        payment.StartRefundProcessing(refund.Id).Should().BeFalse();
    }

    [Fact]
    public void U7_settlement_difference_decides_the_status()
    {
        var open = CashSettlement.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 10, 23), 10000, 9500, 3, Staff, null, Now);
        open.Status.Should().Be(CashSettlementStatus.Open);
        open.DifferencePaise.Should().Be(-500);

        var settled = CashSettlement.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 10, 23), 10000, 10000, 3, Staff, null, Now);
        settled.Status.Should().Be(CashSettlementStatus.Settled);
        settled.DifferencePaise.Should().Be(0);

        var selfResolve = () => open.Settle("Counted again", Staff, Now);
        selfResolve.Should().Throw<DomainException>().Which.Code.Should().Be(PaymentsErrors.SettlementSelfResolution);
        open.Settle("Rider paid the rest", OtherStaff, Now);
        open.Status.Should().Be(CashSettlementStatus.Settled);
    }

    [Fact]
    public void U8_fake_signature_is_checked_in_constant_time_over_the_raw_body()
    {
        const string secret = "unit-secret";
        var body = Encoding.UTF8.GetBytes("""{"id":"evt_1","type":"payment.captured"}""");
        var signature = FakePaymentProvider.Sign(secret, body);

        FakePaymentProvider.VerifyHmac(secret, signature, body).Should().BeTrue();
        var tampered = (byte[])body.Clone();
        tampered[5] ^= 1;
        FakePaymentProvider.VerifyHmac(secret, signature, tampered).Should().BeFalse();
        FakePaymentProvider.VerifyHmac(secret, signature[..^2], body).Should().BeFalse("wrong length");
        FakePaymentProvider.VerifyHmac(secret, "not-hex", body).Should().BeFalse();
    }

    private static Payment Online(long amount)
        => Payment.CreateOnlineAttempt(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), PaymentProviders.Fake, amount, "fake_order_x", Now.AddMinutes(15));

    private static Payment CollectedCod(long amount)
    {
        var payment = Payment.CreateForDelivery(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), PaymentMethod.Cod, amount);
        payment.RecordCollection(amount, CollectionMode.Cash, null, Guid.CreateVersion7(), Now, requireExactAmount: true);
        return payment;
    }

    private static Refund Request(Payment payment, long amount, RefundMethod method)
        => payment.RequestRefund(new RefundDraft(amount, RefundReason.Goodwill, method, Staff, null, method == RefundMethod.Upi ? "priya@okicici" : null, null, null), false, Now);

    private static void Approve(Payment payment, Refund refund) => payment.ApproveRefund(refund.Id, OtherStaff, Now);
}
