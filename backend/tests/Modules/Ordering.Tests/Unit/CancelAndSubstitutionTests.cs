using AwesomeAssertions;
using FreshOrbit.Ordering.Domain.Orders;
using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Ordering.Tests.Unit;

public sealed class CancelAndSubstitutionTests
{
    private static readonly DateTimeOffset At = OrderFactory.PlacedAt.AddHours(1);

    public static TheoryData<OrderStatus, OrderStatus, string?> CustomerCancelCases() => new()
    {
        { OrderStatus.PendingPayment, OrderStatus.PendingPayment, null },
        { OrderStatus.Confirmed, OrderStatus.PendingPayment, "ORDER_NOT_CANCELLABLE" },
        { OrderStatus.Confirmed, OrderStatus.Confirmed, null },
        { OrderStatus.Preparing, OrderStatus.Confirmed, "ORDER_NOT_CANCELLABLE" },
        { OrderStatus.Preparing, OrderStatus.Preparing, null },
        { OrderStatus.Packed, OrderStatus.Preparing, "ORDER_NOT_CANCELLABLE" },
        { OrderStatus.OutForDelivery, OrderStatus.Preparing, "ORDER_NOT_CANCELLABLE" },
        { OrderStatus.Delivered, OrderStatus.Preparing, "ORDER_NOT_CANCELLABLE" },
        { OrderStatus.Cancelled, OrderStatus.Preparing, "INVALID_STATUS_TRANSITION" },
        { OrderStatus.PaymentFailed, OrderStatus.Preparing, "INVALID_STATUS_TRANSITION" },
    };

    [Theory]
    [MemberData(nameof(CustomerCancelCases))]
    public void Customer_cancel_follows_status_and_policy(OrderStatus status, OrderStatus allowedUntil, string? refusal)
    {
        var order = InStatus(status);

        var act = () => order.CancelByCustomer(CustomerCancelReason.ChangedMind, allowedUntil, At.AddHours(1));

        if (refusal is null)
        {
            act();
            order.Status.Should().Be(OrderStatus.Cancelled);
            order.CancelReasonCode.Should().Be("CHANGED_MIND");
            order.DomainEvents.OfType<OrderCancelled>().Should().ContainSingle().Which.ActorType.Should().Be("CUSTOMER");
        }
        else
        {
            act.Should().Throw<DomainException>().Which.Code.Should().Be(refusal);
        }
    }

    [Fact]
    public void Staff_cancel_refuses_pending_payment_and_after_dispatch_and_flags_packed_stock()
    {
        var pending = InStatus(OrderStatus.PendingPayment);
        var refusePending = () => pending.CancelByStaff(StaffCancelReason.Other, null, At);
        refusePending.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");

        var dispatched = InStatus(OrderStatus.OutForDelivery);
        var refuseDispatched = () => dispatched.CancelByStaff(StaffCancelReason.Other, null, At);
        refuseDispatched.Should().Throw<DomainException>().Which.Code.Should().Be("ORDER_NOT_CANCELLABLE");

        var packed = InStatus(OrderStatus.Packed);
        packed.CancelByStaff(StaffCancelReason.QualityIssue, Guid.NewGuid(), At.AddHours(1));
        var cancelled = packed.DomainEvents.OfType<OrderCancelled>().Single();
        cancelled.PreviousStatus.Should().Be("PACKED");
        cancelled.StockConsumed.Should().BeTrue();
        cancelled.ReasonCode.Should().Be("QUALITY_ISSUE");
        cancelled.DeliverySlotId.Should().Be(packed.DeliverySlotId);
    }

    [Fact]
    public void Cancel_closes_active_lines_and_expires_proposals()
    {
        var order = InStatus(OrderStatus.Preparing);
        var (substitution, _) = order.ProposeSubstitution(order.Items[0].Id, OrderFactory.Prawns(), "Prawns", 15, At);

        order.CancelByStaff(StaffCancelReason.OutOfStock, null, At.AddMinutes(1));

        order.Items.Should().OnlyContain(i => i.Status == OrderItemStatus.Cancelled);
        substitution.Status.Should().Be(SubstitutionStatus.Expired);
        order.AmountDuePaise.Should().Be(0);
    }

    [Fact]
    public void Call_me_substitution_waits_and_accept_at_expiry_is_refused()
    {
        var order = InStatus(OrderStatus.Preparing);
        var (substitution, autoAccept) = order.ProposeSubstitution(order.Items[0].Id, OrderFactory.Prawns(), "Prawns", 15, At);

        autoAccept.Should().BeFalse();
        substitution.Status.Should().Be(SubstitutionStatus.Proposed);
        substitution.ExpiresAt.Should().Be(At.AddMinutes(15));
        order.DomainEvents.OfType<SubstitutionProposed>().Should().ContainSingle();

        var late = () => order.AcceptSubstitution(substitution.Id, OrderFactory.Prawns(), OrderActor.Customer(order.CustomerId), substitution.ExpiresAt);
        late.Should().Throw<DomainException>().Which.Code.Should().Be("SUBSTITUTION_EXPIRED");
    }

    [Fact]
    public void Accept_replaces_the_line_with_the_next_line_number_and_records_the_difference()
    {
        var order = InStatus(OrderStatus.Preparing);
        var original = order.Items[0];
        var proposal = OrderFactory.Prawns();
        var (substitution, _) = order.ProposeSubstitution(original.Id, proposal, "Prawns", 15, At);

        var newItem = order.AcceptSubstitution(substitution.Id, proposal, OrderActor.Customer(order.CustomerId), At.AddMinutes(5));

        newItem.Should().NotBeNull();
        newItem!.LineNo.Should().Be(order.Items.Count);
        newItem.SubstitutedFromItemId.Should().Be(original.Id);
        original.Status.Should().Be(OrderItemStatus.Substituted);
        order.Adjustments.Should().ContainSingle(a => a.Kind == AdjustmentKind.Substitution && a.AmountPaise == proposal.EstimatedTotalPaise - original.EstimatedTotalPaise);
        order.DomainEvents.OfType<SubstitutionResolved>().Should().ContainSingle().Which.Status.Should().Be("ACCEPTED");

        order.AcceptSubstitution(substitution.Id, proposal, OrderActor.Customer(order.CustomerId), At.AddMinutes(6)).Should().BeNull();
        var opposite = () => order.RejectSubstitution(substitution.Id, OrderActor.Customer(order.CustomerId), At.AddMinutes(6));
        opposite.Should().Throw<ConflictException>().Which.Code.Should().Be("SUBSTITUTION_NOT_PENDING");
    }

    [Fact]
    public void Reject_and_expiry_short_supply_the_line()
    {
        var order = OrderFactory.Place([OrderFactory.SeerFish(), OrderFactory.Prawns(), OrderFactory.Crabs()]).Preparing();
        var (rejected, _) = order.ProposeSubstitution(order.Items[0].Id, OrderFactory.Crabs(), "Crabs", 15, At);
        var (expiring, _) = order.ProposeSubstitution(order.Items[1].Id, OrderFactory.Crabs(), "Crabs", 15, At);

        order.RejectSubstitution(rejected.Id, OrderActor.Customer(order.CustomerId), At.AddMinutes(1)).Should().BeTrue();
        order.ExpireSubstitution(expiring.Id, At.AddMinutes(14)).Should().BeFalse();
        order.ExpireSubstitution(expiring.Id, At.AddMinutes(15)).Should().BeTrue();

        order.Items[0].Status.Should().Be(OrderItemStatus.ShortSupplied);
        order.Items[1].Status.Should().Be(OrderItemStatus.ShortSupplied);
        expiring.Status.Should().Be(SubstitutionStatus.Expired);
        order.Adjustments.Where(a => a.Kind == AdjustmentKind.ShortSupply).Select(a => a.AmountPaise).Should().BeEquivalentTo([-66950L, -47950L]);
    }

    [Fact]
    public void Auto_similar_accepts_only_within_the_max_line_total_guard()
    {
        var order = OrderFactory.Place([OrderFactory.SeerFish()], preference: SubstitutionPreference.AutoSimilar).Preparing();
        var cheaper = OrderFactory.Prawns();
        var dearer = OrderFactory.Line("KG", 299900, 0, null, 500, true, 0, 0);

        order.ProposeSubstitution(order.Items[0].Id, dearer, "Lobster", 15, At).AutoAccept.Should().BeFalse();
        var other = OrderFactory.Place([OrderFactory.SeerFish()], preference: SubstitutionPreference.AutoSimilar).Preparing();
        other.ProposeSubstitution(other.Items[0].Id, cheaper, "Prawns", 15, At).AutoAccept.Should().BeTrue();
        other.DomainEvents.OfType<SubstitutionProposed>().Should().BeEmpty();
    }

    [Fact]
    public void Substitution_needs_preparing_an_active_line_and_a_preference()
    {
        var none = OrderFactory.Place([OrderFactory.SeerFish()], preference: SubstitutionPreference.None).Preparing();
        var refusedPreference = () => none.ProposeSubstitution(none.Items[0].Id, OrderFactory.Prawns(), "Prawns", 15, At);
        refusedPreference.Should().Throw<DomainException>().Which.Code.Should().Be("SUBSTITUTION_NOT_ALLOWED");

        var confirmed = OrderFactory.Place([OrderFactory.SeerFish()]);
        var refusedStatus = () => confirmed.ProposeSubstitution(confirmed.Items[0].Id, OrderFactory.Prawns(), "Prawns", 15, At);
        refusedStatus.Should().Throw<DomainException>().Which.Code.Should().Be("SUBSTITUTION_NOT_ALLOWED");
    }

    /// <summary>An order driven through the real commands to <paramref name="status"/>.</summary>
    private static Order InStatus(OrderStatus status)
    {
        var online = status is OrderStatus.PendingPayment or OrderStatus.PaymentFailed;
        var order = OrderFactory.Place([OrderFactory.SeerFish(), OrderFactory.Crabs()], online ? PaymentMethod.Online : PaymentMethod.Cod);
        switch (status)
        {
            case OrderStatus.PaymentFailed:
                order.ExpireIfUnpaid(order.PaymentDueAt!.Value);
                break;
            case OrderStatus.Cancelled:
                order.CancelByStaff(StaffCancelReason.Other, null, At);
                break;
            case OrderStatus.Preparing:
                order.Preparing();
                break;
            case OrderStatus.Packed:
                order.Preparing().PackedAsOrdered();
                break;
            case OrderStatus.OutForDelivery:
                order.Preparing().PackedAsOrdered().Dispatch(Guid.NewGuid(), null, At);
                break;
            case OrderStatus.Delivered:
                order.Preparing().PackedAsOrdered().Dispatch(Guid.NewGuid(), null, At);
                order.Deliver(Guid.NewGuid(), Guid.NewGuid(), "INV/26-27/000009", At);
                break;
        }

        order.Status.Should().Be(status);
        return order;
    }
}
