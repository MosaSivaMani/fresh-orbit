using AwesomeAssertions;
using FreshOrbit.Delivery.Domain.Riders;
using FreshOrbit.Delivery.Domain.Shipments;
using FreshOrbit.Delivery.Domain.Shipments.Events;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Delivery.Tests.Unit;

public sealed class ShipmentTests
{
    private static readonly Guid Location = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly SlotDate = new(2026, 10, 26);

    [Fact]
    public void U13_dispatch_needs_packed_order_and_rider()
    {
        var rider = Rider.Create(Location, "Ravi", "+919876543210");
        var unassigned = Shipment.Create(Guid.CreateVersion7(), Location, Guid.CreateVersion7(), Now);
        unassigned.MarkPacked(Now, true);
        var notPacked = Shipment.Create(Guid.CreateVersion7(), Location, Guid.CreateVersion7(), Now);
        notPacked.AssignRider(rider);

        var withoutRider = () => unassigned.Dispatch(rider, 2.5m, "1234", Now);
        var withoutPacking = () => notPacked.Dispatch(rider, 2.5m, "1234", Now);

        withoutRider.Should().Throw<ConflictException>().Which.Code.Should().Be("SHIPMENT_NOT_DISPATCHABLE");
        withoutPacking.Should().Throw<ConflictException>().Which.Code.Should().Be("SHIPMENT_NOT_DISPATCHABLE");
    }

    [Fact]
    public void Dispatch_above_four_degrees_is_accepted_with_a_warning()
    {
        var (shipment, _) = Dispatched(4.1m);

        shipment.Status.Should().Be(ShipmentStatus.OutForDelivery);
        shipment.TemperatureWarning.Should().BeTrue();
        shipment.DeliveryCode.Should().MatchRegex("^[0-9]{4}$");
        shipment.DomainEvents.OfType<ShipmentDispatched>().Single().TemperatureWarning.Should().BeTrue();
    }

    [Fact]
    public void U14_five_wrong_codes_lock_delivery()
    {
        var (shipment, _) = Dispatched();
        var wrong = shipment.DeliveryCode == "0000" ? "1111" : "0000";

        for (var i = 0; i < 5; i++)
        {
            shipment.Deliver(wrong, null, Now).Should().Be(DeliverOutcome.CodeInvalid);
        }

        var right = () => shipment.Deliver(shipment.DeliveryCode!, null, Now);
        right.Should().Throw<DomainException>().Which.Code.Should().Be("DELIVERY_CODE_LOCKED");
        shipment.DeliveryCodeFailures.Should().Be(5);
        shipment.Status.Should().Be(ShipmentStatus.OutForDelivery);
    }

    [Fact]
    public void Right_code_delivers_with_cash_and_raises_ShipmentDelivered()
    {
        var (shipment, rider) = Dispatched();

        shipment.Deliver(shipment.DeliveryCode!, new CashReceipt(66950, CashMode.Upi, "UPI123"), Now).Should().Be(DeliverOutcome.Delivered);

        shipment.Status.Should().Be(ShipmentStatus.Delivered);
        shipment.CashCollectedPaise.Should().Be(66950);
        var delivered = shipment.DomainEvents.OfType<ShipmentDelivered>().Single();
        delivered.RiderId.Should().Be(rider.Id);
        delivered.CashCollectedPaise.Should().Be(66950);
        delivered.CashMode.Should().Be(CashMode.Upi);
        delivered.UpiReference.Should().Be("UPI123");
    }

    [Fact]
    public void U15_reattempt_refused_after_max_attempts_or_on_another_local_day()
    {
        var (shipment, _) = Dispatched();
        shipment.RecordFailedAttempt(AttemptOutcome.CustomerUnavailable, null, Now);

        var nextDay = () => shipment.Reattempt(3, SlotDate.AddDays(1), SlotDate);
        nextDay.Should().Throw<DomainException>().Which.Code.Should().Be("REATTEMPT_NOT_ALLOWED");

        shipment.Reattempt(3, SlotDate, SlotDate);
        shipment.RecordFailedAttempt(AttemptOutcome.Refused, null, Now);
        shipment.Reattempt(3, SlotDate, SlotDate);
        shipment.RecordFailedAttempt(AttemptOutcome.AddressNotFound, null, Now);

        var fourth = () => shipment.Reattempt(3, SlotDate, SlotDate);
        fourth.Should().Throw<DomainException>().Which.Code.Should().Be("REATTEMPT_NOT_ALLOWED");
        shipment.Return(Now);
        shipment.Status.Should().Be(ShipmentStatus.Returned);
    }

    [Fact]
    public void Reattempt_resets_the_code_failure_counter()
    {
        var (shipment, _) = Dispatched();
        for (var i = 0; i < 5; i++)
        {
            shipment.Deliver(shipment.DeliveryCode == "0000" ? "1111" : "0000", null, Now);
        }

        shipment.RecordFailedAttempt(AttemptOutcome.Other, "Code locked", Now);
        shipment.Reattempt(3, SlotDate, SlotDate);

        shipment.DeliveryCodeFailures.Should().Be(0);
        shipment.Deliver(shipment.DeliveryCode!, null, Now).Should().Be(DeliverOutcome.Delivered);
    }

    [Fact]
    public void U16_attempts_are_numbered_without_gaps()
    {
        var (shipment, _) = Dispatched();
        shipment.RecordFailedAttempt(AttemptOutcome.CustomerUnavailable, null, Now);
        shipment.Reattempt(3, SlotDate, SlotDate);

        shipment.Deliver(shipment.DeliveryCode!, null, Now);

        shipment.Attempts.Select(a => (a.AttemptNo, a.Outcome)).Should().Equal((1, AttemptOutcome.CustomerUnavailable), (2, AttemptOutcome.Delivered));
    }

    [Fact]
    public void Other_outcome_needs_notes()
    {
        var (shipment, _) = Dispatched();

        var act = () => shipment.RecordFailedAttempt(AttemptOutcome.Other, " ", Now);

        act.Should().Throw<RefusalException>().Which.Errors.Single().Field.Should().Be("notes");
    }

    [Fact]
    public void U17_inactive_rider_or_rider_of_another_location_is_not_available()
    {
        var shipment = Shipment.Create(Guid.CreateVersion7(), Location, Guid.CreateVersion7(), Now);
        var inactive = Rider.Create(Location, "Ravi", "+919876543210");
        inactive.Update("Ravi", "+919876543210", isActive: false);
        var elsewhere = Rider.Create(Guid.CreateVersion7(), "Kumar", "+919876543211");

        var assignInactive = () => shipment.AssignRider(inactive);
        var assignElsewhere = () => shipment.AssignRider(elsewhere);

        assignInactive.Should().Throw<DomainException>().Which.Code.Should().Be("RIDER_NOT_AVAILABLE");
        assignElsewhere.Should().Throw<DomainException>().Which.Code.Should().Be("RIDER_NOT_AVAILABLE");
    }

    [Fact]
    public void Terminal_shipment_refuses_commands()
    {
        var shipment = Shipment.Create(Guid.CreateVersion7(), Location, Guid.CreateVersion7(), Now);
        shipment.Cancel(Now).Should().BeTrue();

        var assign = () => shipment.AssignRider(Rider.Create(Location, "Ravi", "+919876543210"));
        var returnIt = () => shipment.Return(Now);

        assign.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
        returnIt.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
        shipment.Cancel(Now).Should().BeFalse();
    }

    [Fact]
    public void Cancel_after_dispatch_requests_a_recall_without_changing_status()
    {
        var (shipment, _) = Dispatched();
        shipment.ClearEvents();

        shipment.Cancel(Now).Should().BeFalse();
        shipment.RequestRecall().Should().BeTrue();

        shipment.Status.Should().Be(ShipmentStatus.OutForDelivery);
        shipment.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ShipmentRecallRequired>();
    }

    private static (Shipment Shipment, Rider Rider) Dispatched(decimal temperature = 2.5m)
    {
        var rider = Rider.Create(Location, "Ravi", "+919876543210");
        var shipment = Shipment.Create(Guid.CreateVersion7(), Location, Guid.CreateVersion7(), Now);
        shipment.MarkPacked(Now, true);
        shipment.AssignRider(rider);
        shipment.Dispatch(rider, temperature, Shipment.NewDeliveryCode(), Now);
        return (shipment, rider);
    }
}
