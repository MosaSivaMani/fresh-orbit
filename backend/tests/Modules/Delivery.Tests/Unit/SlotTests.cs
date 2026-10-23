using AwesomeAssertions;
using FreshOrbit.Delivery.Application.Handlers;
using FreshOrbit.Delivery.Application.Templates;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Domain.Slots.Events;
using FreshOrbit.Delivery.Domain.Templates;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Delivery.Tests.Unit;

public sealed class SlotTests
{
    private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
    private static readonly Guid Location = Guid.CreateVersion7();

    // Monday 2026-10-26.
    private static readonly DateOnly Monday = new(2026, 10, 26);

    [Fact]
    public void U1_generation_converts_local_template_times_to_utc_with_cutoff()
    {
        var template = SlotTemplate.Create(Location, 1, new TimeOnly(8, 0), new TimeOnly(10, 0), 10, 120);
        var now = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        var slots = SlotSchedule.Generate(Location, Ist, [template], new HashSet<DateOnly>(), new HashSet<DateTimeOffset>(), now);

        var slot = slots.Should().ContainSingle().Subject;
        slot.SlotDate.Should().Be(Monday);
        slot.StartAt.Should().Be(new DateTimeOffset(2026, 10, 26, 2, 30, 0, TimeSpan.Zero));
        slot.EndAt.Should().Be(new DateTimeOffset(2026, 10, 26, 4, 30, 0, TimeSpan.Zero));
        slot.CutoffAt.Should().Be(new DateTimeOffset(2026, 10, 26, 0, 30, 0, TimeSpan.Zero));
        slot.Capacity.Should().Be(10);
        slot.SlotTemplateId.Should().Be(template.Id);
    }

    [Fact]
    public void U2_date_with_a_closure_gets_no_slot()
    {
        var template = SlotTemplate.Create(Location, 1, new TimeOnly(8, 0), new TimeOnly(10, 0), 10, 120);
        var now = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        SlotSchedule.Generate(Location, Ist, [template], new HashSet<DateOnly> { Monday }, new HashSet<DateTimeOffset>(), now).Should().BeEmpty();
    }

    [Fact]
    public void U3_todays_slot_whose_cutoff_passed_is_not_generated()
    {
        var template = SlotTemplate.Create(Location, 1, new TimeOnly(8, 0), new TimeOnly(10, 0), 10, 120);
        var atCutoff = new DateTimeOffset(2026, 10, 26, 0, 30, 0, TimeSpan.Zero);

        SlotSchedule.Generate(Location, Ist, [template], new HashSet<DateOnly>(), new HashSet<DateTimeOffset>(), atCutoff).Should().BeEmpty();
        SlotSchedule.Generate(Location, Ist, [template], new HashSet<DateOnly>(), new HashSet<DateTimeOffset>(), atCutoff.AddMinutes(-1)).Should().ContainSingle();
    }

    [Fact]
    public void U4_existing_slot_at_the_same_start_is_not_regenerated()
    {
        var template = SlotTemplate.Create(Location, 1, new TimeOnly(8, 0), new TimeOnly(10, 0), 10, 120);
        var now = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var existing = new HashSet<DateTimeOffset> { new(2026, 10, 26, 8, 0, 0, TimeSpan.FromHours(5.5)) };

        SlotSchedule.Generate(Location, Ist, [template], new HashSet<DateOnly>(), existing, now).Should().BeEmpty();
    }

    [Fact]
    public void Generation_covers_seven_local_dates_and_skips_inactive_templates()
    {
        var templates = Enumerable.Range(1, 7).Select(d => SlotTemplate.Create(Location, d, new TimeOnly(18, 0), new TimeOnly(20, 0), 5, 60)).ToList();
        var inactive = SlotTemplate.Create(Location, 1, new TimeOnly(6, 0), new TimeOnly(7, 0), 5, 60);
        inactive.Update(1, new TimeOnly(6, 0), new TimeOnly(7, 0), 5, 60, isActive: false);
        var now = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);

        var slots = SlotSchedule.Generate(Location, Ist, [.. templates, inactive], new HashSet<DateOnly>(), new HashSet<DateTimeOffset>(), now);

        slots.Select(s => s.SlotDate).Should().Equal(Enumerable.Range(0, 7).Select(Monday.AddDays));
    }

    [Fact]
    public void U5_booking_takes_one_unit()
    {
        var slot = Slot(capacity: 10, booked: 9);

        slot.Book(Location, Now);

        slot.BookedCount.Should().Be(10);
    }

    [Fact]
    public void U6_full_slot_is_refused()
    {
        var slot = Slot(capacity: 10, booked: 10);

        slot.GetBookingRefusal(Location, Now).Should().Be("SLOT_FULL");
        var act = () => slot.Book(Location, Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("SLOT_FULL");
        slot.BookedCount.Should().Be(10);
    }

    [Fact]
    public void U7_cutoff_is_checked_before_full()
    {
        var slot = Slot(capacity: 10, booked: 10);

        slot.GetBookingRefusal(Location, slot.CutoffAt).Should().Be("SLOT_CUTOFF_PASSED");
    }

    [Fact]
    public void U8_closed_slot_or_another_location_is_not_available()
    {
        var slot = Slot();
        slot.GetBookingRefusal(Guid.CreateVersion7(), Now).Should().Be("SLOT_NOT_AVAILABLE");

        slot.Close(SlotClosedReason.Manual);
        slot.GetBookingRefusal(Location, Now).Should().Be("SLOT_NOT_AVAILABLE");
    }

    [Fact]
    public void U9_release_never_goes_below_zero()
    {
        var slot = Slot(booked: 1);

        slot.Release();
        slot.Release();

        slot.BookedCount.Should().Be(0);
    }

    [Fact]
    public void U10_capacity_below_booked_is_refused()
    {
        var slot = Slot(capacity: 10, booked: 4);

        var act = () => slot.ChangeCapacity(3);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("SLOT_CAPACITY_BELOW_BOOKED");
        slot.ChangeCapacity(4);
        slot.Capacity.Should().Be(4);
    }

    [Fact]
    public void U11_overlapping_template_is_refused_and_adjacent_is_allowed()
    {
        var existing = SlotTemplate.Create(Location, 1, new TimeOnly(8, 0), new TimeOnly(10, 0), 10, 60);

        var overlap = () => SlotTemplateService.EnsureNoOverlap([existing], Guid.CreateVersion7(), 1, new TimeOnly(9, 30), new TimeOnly(11, 0));
        var adjacent = () => SlotTemplateService.EnsureNoOverlap([existing], Guid.CreateVersion7(), 1, new TimeOnly(10, 0), new TimeOnly(12, 0));
        var otherDay = () => SlotTemplateService.EnsureNoOverlap([existing], Guid.CreateVersion7(), 2, new TimeOnly(9, 30), new TimeOnly(11, 0));

        overlap.Should().Throw<ConflictException>().Which.Code.Should().Be("SLOT_TEMPLATE_OVERLAP");
        adjacent.Should().NotThrow();
        otherDay.Should().NotThrow();
    }

    [Fact]
    public void Template_validation_lists_every_invalid_field()
    {
        var act = () => SlotTemplate.Create(Location, 8, new TimeOnly(8, 10), new TimeOnly(7, 0), 0, 3000);

        act.Should().Throw<RefusalException>().Which.Errors.Select(e => e.Field)
            .Should().BeEquivalentTo(["dayOfWeek", "startTime", "endTime", "capacity", "cutoffMinutes"]);
    }

    [Fact]
    public void U12_template_change_keeps_times_of_booked_slots_and_rewrites_unbooked_ones()
    {
        var template = SlotTemplate.Create(Location, 1, new TimeOnly(8, 0), new TimeOnly(10, 0), 10, 120);
        var now = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var booked = SlotSchedule.Generate(Location, Ist, [template], new HashSet<DateOnly>(), new HashSet<DateTimeOffset>(), now).Single();
        var unbooked = SlotSchedule.Generate(Location, Ist, [template], new HashSet<DateOnly>(), new HashSet<DateTimeOffset>(), now.AddDays(-7)).Single();
        for (var i = 0; i < 7; i++)
        {
            booked.Book(Location, now);
        }

        template.Update(1, new TimeOnly(9, 0), new TimeOnly(11, 0), 5, 60, isActive: true);
        SlotTemplateChangedHandler.ApplyTemplate(template, [booked, unbooked], Ist);

        booked.StartAt.Should().Be(new DateTimeOffset(2026, 10, 26, 2, 30, 0, TimeSpan.Zero));
        booked.Capacity.Should().Be(7);
        unbooked.StartAt.Should().Be(new DateTimeOffset(2026, 10, 19, 3, 30, 0, TimeSpan.Zero));
        unbooked.CutoffAt.Should().Be(new DateTimeOffset(2026, 10, 19, 2, 30, 0, TimeSpan.Zero));
        unbooked.Capacity.Should().Be(5);
    }

    [Fact]
    public void Deactivated_template_closes_unbooked_slots_and_freezes_booked_ones()
    {
        var template = SlotTemplate.Create(Location, 1, new TimeOnly(8, 0), new TimeOnly(10, 0), 10, 120);
        var now = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var booked = SlotSchedule.Generate(Location, Ist, [template], new HashSet<DateOnly>(), new HashSet<DateTimeOffset>(), now).Single();
        var unbooked = SlotSchedule.Generate(Location, Ist, [template], new HashSet<DateOnly>(), new HashSet<DateTimeOffset>(), now.AddDays(-7)).Single();
        booked.Book(Location, now);

        template.Update(1, new TimeOnly(8, 0), new TimeOnly(10, 0), 10, 120, isActive: false);
        SlotTemplateChangedHandler.ApplyTemplate(template, [booked, unbooked], Ist);

        booked.Status.Should().Be(SlotStatus.Open);
        booked.Capacity.Should().Be(1);
        unbooked.Status.Should().Be(SlotStatus.Closed);
        unbooked.ClosedReason.Should().Be(SlotClosedReason.TemplateInactive);
    }

    [Fact]
    public void U18_displayed_status_precedence()
    {
        var slot = Slot(capacity: 1, booked: 1);
        slot.GetDisplayStatus(Now).Should().Be(SlotDisplayStatus.Full);
        slot.GetDisplayStatus(slot.CutoffAt).Should().Be(SlotDisplayStatus.CutoffPassed);
        slot.Close(SlotClosedReason.Manual);
        slot.GetDisplayStatus(slot.CutoffAt).Should().Be(SlotDisplayStatus.Closed);
        Slot().GetDisplayStatus(Now).Should().Be(SlotDisplayStatus.Available);
    }

    [Fact]
    public void Closing_a_booked_slot_raises_SlotClosedWithBookings()
    {
        var empty = Slot();
        var booked = Slot(booked: 2);

        empty.Close(SlotClosedReason.Closure);
        booked.Close(SlotClosedReason.Closure);

        empty.DomainEvents.Should().BeEmpty();
        booked.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SlotClosedWithBookings>().Which.BookedCount.Should().Be(2);
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private static DeliverySlot Slot(int capacity = 10, int booked = 0)
    {
        var start = new DateTimeOffset(2026, 10, 26, 2, 30, 0, TimeSpan.Zero);
        var slot = DeliverySlot.Create(Location, null, Monday, start, start.AddHours(2), start.AddHours(-2), capacity);
        for (var i = 0; i < booked; i++)
        {
            slot.Book(Location, Now);
        }

        return slot;
    }
}
