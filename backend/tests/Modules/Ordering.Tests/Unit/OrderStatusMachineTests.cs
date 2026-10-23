using AwesomeAssertions;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Ordering.Tests.Unit;

public sealed class OrderStatusMachineTests
{
    private static readonly (OrderStatus From, OrderStatus To)[] Allowed =
    [
        (OrderStatus.PendingPayment, OrderStatus.Confirmed),
        (OrderStatus.PendingPayment, OrderStatus.PaymentFailed),
        (OrderStatus.PendingPayment, OrderStatus.Cancelled),
        (OrderStatus.Confirmed, OrderStatus.Preparing),
        (OrderStatus.Confirmed, OrderStatus.Cancelled),
        (OrderStatus.Preparing, OrderStatus.Packed),
        (OrderStatus.Preparing, OrderStatus.Cancelled),
        (OrderStatus.Packed, OrderStatus.OutForDelivery),
        (OrderStatus.Packed, OrderStatus.Cancelled),
        (OrderStatus.OutForDelivery, OrderStatus.Delivered),
        (OrderStatus.OutForDelivery, OrderStatus.DeliveryFailed),
        (OrderStatus.DeliveryFailed, OrderStatus.OutForDelivery),
        (OrderStatus.DeliveryFailed, OrderStatus.Returned),
    ];

    public static TheoryData<OrderStatus, OrderStatus> NotAllowed()
    {
        var data = new TheoryData<OrderStatus, OrderStatus>();
        foreach (var from in Enum.GetValues<OrderStatus>())
        {
            foreach (var to in Enum.GetValues<OrderStatus>())
            {
                if (!Allowed.Contains((from, to)))
                {
                    data.Add(from, to);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(NotAllowed))]
    public void Every_other_pair_is_not_a_transition(OrderStatus from, OrderStatus to)
        => Order.CanMove(from, to).Should().BeFalse();

    [Fact]
    public void Every_allowed_pair_is_a_transition()
        => Allowed.Should().OnlyContain(pair => Order.CanMove(pair.From, pair.To));

    [Fact]
    public void Cod_order_is_placed_confirmed_with_history_and_events()
    {
        var order = OrderFactory.ApiExample();

        order.Status.Should().Be(OrderStatus.Confirmed);
        order.ConfirmedAt.Should().Be(order.PlacedAt);
        order.History.Should().ContainSingle(h => h.FromStatus == null && h.ToStatus == OrderStatus.Confirmed);
        order.DomainEvents.OfType<OrderPlaced>().Should().ContainSingle().Which.Status.Should().Be("CONFIRMED");
        order.DomainEvents.OfType<OrderStatusChanged>().Should().ContainSingle().Which.To.Should().Be("CONFIRMED");
        order.Items.Select(i => i.LineNo).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Online_order_waits_for_payment_then_confirms()
    {
        var order = OrderFactory.ApiExample(PaymentMethod.Online);
        order.Status.Should().Be(OrderStatus.PendingPayment);
        order.PaymentDueAt.Should().Be(order.PlacedAt.AddMinutes(15));
        order.ClearEvents();

        var outcome = order.ConfirmPayment(Guid.NewGuid(), order.TotalPaise, order.PlacedAt.AddMinutes(2));

        outcome.Should().Be(PaymentCaptureOutcome.Confirmed);
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.DomainEvents.OfType<OrderConfirmed>().Should().ContainSingle();
        AssertTransition(order, OrderStatus.PendingPayment, OrderStatus.Confirmed);
    }

    [Fact]
    public void Unpaid_order_fails_at_the_due_time()
    {
        var order = OrderFactory.ApiExample(PaymentMethod.Online);
        order.ClearEvents();

        order.ExpireIfUnpaid(order.PaymentDueAt!.Value.AddSeconds(-1)).Should().BeFalse();
        order.ExpireIfUnpaid(order.PaymentDueAt!.Value).Should().BeTrue();

        order.Status.Should().Be(OrderStatus.PaymentFailed);
        order.Items.Should().OnlyContain(i => i.Status == OrderItemStatus.Cancelled);
        order.DomainEvents.OfType<OrderPaymentFailed>().Should().ContainSingle().Which.Reason.Should().Be("PAYMENT_TIMEOUT");
        AssertTransition(order, OrderStatus.PendingPayment, OrderStatus.PaymentFailed);
    }

    [Fact]
    public void Full_delivery_path_moves_through_every_status()
    {
        var order = OrderFactory.ApiExample().Preparing();
        AssertTransition(order, OrderStatus.Confirmed, OrderStatus.Preparing);
        order.PackedAsOrdered();
        AssertTransition(order, OrderStatus.Preparing, OrderStatus.Packed);

        var shipmentId = Guid.NewGuid();
        order.Dispatch(shipmentId, null, order.PlacedAt.AddHours(2)).Should().Be(DeliveryEventOutcome.Applied);
        AssertTransition(order, OrderStatus.Packed, OrderStatus.OutForDelivery);
        order.FailDeliveryAttempt("CUSTOMER_UNAVAILABLE", order.PlacedAt.AddHours(3)).Should().Be(DeliveryEventOutcome.Applied);
        AssertTransition(order, OrderStatus.OutForDelivery, OrderStatus.DeliveryFailed);
        order.Reattempt(order.PlacedAt.AddHours(4)).Should().Be(DeliveryEventOutcome.Applied);
        AssertTransition(order, OrderStatus.DeliveryFailed, OrderStatus.OutForDelivery);
        order.Deliver(shipmentId, Guid.NewGuid(), "INV/26-27/000001", order.PlacedAt.AddHours(5)).Should().Be(DeliveryEventOutcome.Applied);
        AssertTransition(order, OrderStatus.OutForDelivery, OrderStatus.Delivered);
        order.DomainEvents.OfType<OrderDelivered>().Should().ContainSingle();
    }

    [Fact]
    public void Failed_delivery_can_be_returned()
    {
        var order = OrderFactory.ApiExample().Preparing().PackedAsOrdered();
        var shipmentId = Guid.NewGuid();
        order.Dispatch(shipmentId, null, order.PlacedAt.AddHours(2));
        order.FailDeliveryAttempt(null, order.PlacedAt.AddHours(3));

        order.Return(shipmentId, order.PlacedAt.AddHours(4)).Should().Be(DeliveryEventOutcome.Applied);

        AssertTransition(order, OrderStatus.DeliveryFailed, OrderStatus.Returned);
        order.DomainEvents.OfType<OrderReturned>().Should().ContainSingle();
    }

    [Fact]
    public void Delivery_events_in_the_target_status_are_no_ops_and_invalid_ones_are_rejected()
    {
        var order = OrderFactory.ApiExample().Preparing().PackedAsOrdered();
        var shipmentId = Guid.NewGuid();
        order.Dispatch(shipmentId, null, order.PlacedAt.AddHours(2));
        var historyCount = order.History.Count;

        order.Dispatch(shipmentId, null, order.PlacedAt.AddHours(2)).Should().Be(DeliveryEventOutcome.AlreadyApplied);
        order.Return(shipmentId, order.PlacedAt.AddHours(2)).Should().Be(DeliveryEventOutcome.Rejected);
        order.History.Should().HaveCount(historyCount);

        var cancelled = OrderFactory.ApiExample().Preparing().PackedAsOrdered();
        cancelled.CancelByStaff(StaffCancelReason.CustomerRequest, null, cancelled.PlacedAt.AddHours(1));
        cancelled.Dispatch(Guid.NewGuid(), null, cancelled.PlacedAt.AddHours(2)).Should().Be(DeliveryEventOutcome.Rejected);
        cancelled.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void A_command_outside_the_machine_is_refused_with_invalid_status_transition()
    {
        var order = OrderFactory.ApiExample(PaymentMethod.Online);

        var act = () => order.StartPreparing(OrderFactory.Staff, order.PlacedAt);

        act.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
    }

    [Fact]
    public void Capture_after_failure_or_by_another_payment_is_reported_for_refund()
    {
        var failed = OrderFactory.ApiExample(PaymentMethod.Online);
        failed.ExpireIfUnpaid(failed.PaymentDueAt!.Value);
        failed.ClearEvents();

        failed.ConfirmPayment(Guid.NewGuid(), 100, failed.PlacedAt.AddMinutes(20)).Should().Be(PaymentCaptureOutcome.ClosedOrderPayment);
        failed.Status.Should().Be(OrderStatus.PaymentFailed);
        failed.DomainEvents.OfType<PaymentReceivedForClosedOrder>().Should().ContainSingle().Which.OrderStatus.Should().Be("PAYMENT_FAILED");

        var paid = OrderFactory.ApiExample(PaymentMethod.Online);
        var paymentId = Guid.NewGuid();
        paid.ConfirmPayment(paymentId, paid.TotalPaise, paid.PlacedAt.AddMinutes(1));
        paid.ConfirmPayment(paymentId, paid.TotalPaise, paid.PlacedAt.AddMinutes(1)).Should().Be(PaymentCaptureOutcome.AlreadyConfirmed);
        paid.ConfirmPayment(Guid.NewGuid(), paid.TotalPaise, paid.PlacedAt.AddMinutes(2)).Should().Be(PaymentCaptureOutcome.ClosedOrderPayment);
    }

    [Fact]
    public void Payment_failure_before_the_due_time_or_with_another_attempt_changes_nothing()
    {
        var order = OrderFactory.ApiExample(PaymentMethod.Online);
        var due = order.PaymentDueAt!.Value;

        order.FailPayment(hasOtherActiveAttempt: false, due.AddMinutes(-1)).Should().BeFalse();
        order.FailPayment(hasOtherActiveAttempt: true, due).Should().BeFalse();
        order.Status.Should().Be(OrderStatus.PendingPayment);

        order.FailPayment(hasOtherActiveAttempt: false, due).Should().BeTrue();
        order.Status.Should().Be(OrderStatus.PaymentFailed);
    }

    private static void AssertTransition(Order order, OrderStatus from, OrderStatus to)
    {
        order.Status.Should().Be(to);
        var last = order.History[^1];
        last.FromStatus.Should().Be(from);
        last.ToStatus.Should().Be(to);
        order.DomainEvents.OfType<OrderStatusChanged>().Should().Contain(e => e.To == EnumCode(to));
    }

    private static string EnumCode(OrderStatus status) => SharedKernel.Codes.EnumCodes.ToCode(status);
}
