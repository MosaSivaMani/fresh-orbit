using AwesomeAssertions;
using FreshOrbit.Delivery.Application.Handlers;
using FreshOrbit.Delivery.Application.Templates;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Infrastructure.Jobs;
using FreshOrbit.Delivery.Tests.Support;
using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Delivery.Tests.Integration;

public sealed class SlotGenerationTests(DeliveryDatabase database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Generation_skips_a_closed_local_date_across_the_IST_day_boundary()
    {
        // 2026-10-26 01:30 IST. A 00:00–02:00 IST slot starts at 18:30 UTC on the previous UTC day.
        await using var harness = new DeliveryHarness(database, new DateTimeOffset(2026, 10, 25, 20, 0, 0, TimeSpan.Zero));
        var location = harness.Locations.AddLocation();
        for (var day = 1; day <= 7; day++)
        {
            await harness.AddTemplateAsync(location.Id, day, new TimeOnly(0, 0), new TimeOnly(2, 0), cutoffMinutes: 0);
        }

        var closed = new DateOnly(2026, 10, 28);
        harness.Locations.AddClosure(location.Id, closed);

        var inserted = await RunJobAsync(harness);

        var slots = await harness.SlotsOfAsync(location.Id);
        inserted.Should().Be(5);
        slots.Select(s => s.SlotDate).Should().Equal(
            new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 29), new DateOnly(2026, 10, 30), new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 1));
        slots[0].StartAt.Should().Be(new DateTimeOffset(2026, 10, 26, 18, 30, 0, TimeSpan.Zero));

        // The slot of local 29 Oct starts on UTC 28 Oct (the closed local date) and is still generated.
        slots[1].StartAt.Should().Be(new DateTimeOffset(2026, 10, 28, 18, 30, 0, TimeSpan.Zero));
        slots[1].CutoffAt.Should().Be(slots[1].StartAt);
    }

    [Fact]
    public async Task I3_I4_generation_creates_two_slots_per_open_date_and_a_second_run_inserts_nothing()
    {
        // 05:30 IST: before every cutoff of today's 08:00 slot.
        await using var harness = new DeliveryHarness(database, new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero));
        var location = harness.Locations.AddLocation();
        for (var day = 1; day <= 7; day++)
        {
            await harness.AddTemplateAsync(location.Id, day, new TimeOnly(8, 0), new TimeOnly(10, 0));
            await harness.AddTemplateAsync(location.Id, day, new TimeOnly(18, 0), new TimeOnly(20, 0));
        }

        harness.Locations.AddClosure(location.Id, new DateOnly(2026, 10, 28));

        var first = await RunJobAsync(harness);
        var second = await RunJobAsync(harness);

        first.Should().Be(12);
        second.Should().Be(0);
        var slots = await harness.SlotsOfAsync(location.Id);
        slots.Should().HaveCount(12).And.OnlyContain(s => s.SlotDate != new DateOnly(2026, 10, 28) && s.BookedCount == 0 && s.Status == SlotStatus.Open);
    }

    [Fact]
    public async Task Inactive_location_gets_no_slots()
    {
        await using var harness = new DeliveryHarness(database, new DateTimeOffset(2026, 10, 25, 18, 0, 0, TimeSpan.Zero));
        var location = harness.Locations.AddLocation(status: Locations.Public.FulfillmentLocationStatus.Paused);
        await harness.AddTemplateAsync(location.Id, 1, new TimeOnly(8, 0), new TimeOnly(10, 0));

        await RunJobAsync(harness);

        (await harness.SlotsOfAsync(location.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task I12_template_update_through_the_outbox_rewrites_unbooked_slots_and_keeps_booked_ones()
    {
        await using var harness = new DeliveryHarness(database, new DateTimeOffset(2026, 10, 25, 18, 0, 0, TimeSpan.Zero));
        var location = harness.Locations.AddLocation();
        var monday = await harness.InScopeAsync(sp => sp.GetRequiredService<SlotTemplateService>()
            .CreateAsync(new CreateSlotTemplateRequest(location.Id, 1, "08:00", "10:00", 10, 120), Ct));
        var tuesday = await harness.InScopeAsync(sp => sp.GetRequiredService<SlotTemplateService>()
            .CreateAsync(new CreateSlotTemplateRequest(location.Id, 2, "08:00", "10:00", 10, 120), Ct));
        await DispatchOutboxAsync(harness);
        var created = await harness.SlotsOfAsync(location.Id);
        created.Should().HaveCount(2);
        var booked = created.Single(s => s.SlotTemplateId == monday.Id);
        var unbooked = created.Single(s => s.SlotTemplateId == tuesday.Id);
        booked.StartAt.Should().Be(new DateTimeOffset(2026, 10, 26, 2, 30, 0, TimeSpan.Zero));
        await harness.HandleAsync(async sp =>
        {
            var slot = await sp.GetRequiredService<IDeliverySlotRepository>().GetAsync(booked.Id, Ct);
            slot!.Book(location.Id, harness.Clock.UtcNow);
        });

        foreach (var template in new[] { monday, tuesday })
        {
            var current = await harness.FindAsync<Domain.Templates.SlotTemplate>(template.Id);
            await harness.InScopeAsync(sp => sp.GetRequiredService<SlotTemplateService>()
                .UpdateAsync(template.Id, new UpdateSlotTemplateRequest(template.DayOfWeek, "09:00", "11:00", 4, 60, true), current!.Version, Ct));
            await DispatchOutboxAsync(harness);
        }

        // The booked Monday slot keeps its times; generation adds the template's new Monday time next to it.
        var slots = await harness.SlotsOfAsync(location.Id);
        slots.Should().HaveCount(3);
        slots.Should().ContainSingle(s => s.StartAt == new DateTimeOffset(2026, 10, 26, 3, 30, 0, TimeSpan.Zero) && s.BookedCount == 0);
        var kept = slots.Single(s => s.Id == booked.Id);
        kept.StartAt.Should().Be(booked.StartAt);
        kept.CutoffAt.Should().Be(booked.CutoffAt);
        kept.Capacity.Should().Be(4);
        var rewritten = slots.Single(s => s.Id == unbooked.Id);
        rewritten.StartAt.Should().Be(new DateTimeOffset(2026, 10, 27, 3, 30, 0, TimeSpan.Zero));
        rewritten.CutoffAt.Should().Be(new DateTimeOffset(2026, 10, 27, 2, 30, 0, TimeSpan.Zero));
        rewritten.Capacity.Should().Be(4);
    }

    [Fact]
    public async Task I5_closure_closes_slots_of_the_date_raises_alert_for_bookings_and_removal_reopens_them()
    {
        await using var harness = new DeliveryHarness(database, new DateTimeOffset(2026, 10, 25, 18, 0, 0, TimeSpan.Zero));
        var location = harness.Locations.AddLocation();
        var date = new DateOnly(2026, 10, 27);
        var booked = await harness.AddSlotAsync(location.Id, new DateTimeOffset(2026, 10, 27, 2, 30, 0, TimeSpan.Zero), booked: 2, slotDate: date);
        var empty = await harness.AddSlotAsync(location.Id, new DateTimeOffset(2026, 10, 27, 12, 30, 0, TimeSpan.Zero), slotDate: date);
        var otherDay = await harness.AddSlotAsync(location.Id, new DateTimeOffset(2026, 10, 28, 2, 30, 0, TimeSpan.Zero), slotDate: date.AddDays(1));

        harness.Locations.AddClosure(location.Id, date);
        await harness.HandleAsync(sp => sp.GetRequiredService<LocationEventHandlers>().OnClosureAddedAsync(location.Id, date, Ct));
        await harness.HandleAsync(sp => sp.GetRequiredService<LocationEventHandlers>().OnClosureAddedAsync(location.Id, date, Ct));

        var closed = await harness.SlotsOfAsync(location.Id);
        closed.Single(s => s.Id == booked.Id).ClosedReason.Should().Be(SlotClosedReason.Closure);
        closed.Single(s => s.Id == empty.Id).Status.Should().Be(SlotStatus.Closed);
        closed.Single(s => s.Id == otherDay.Id).Status.Should().Be(SlotStatus.Open);
        (await harness.OutboxEventsForAsync(booked.Id)).Should().Equal("SlotClosedWithBookings");
        (await harness.OutboxEventsForAsync(empty.Id)).Should().BeEmpty();

        harness.Locations.RemoveClosure(location.Id, date);
        await harness.HandleAsync(sp => sp.GetRequiredService<LocationEventHandlers>().OnClosureRemovedAsync(location.Id, date, Ct));

        (await harness.SlotsOfAsync(location.Id)).Should().OnlyContain(s => s.Status == SlotStatus.Open && s.ClosedReason == null);
    }

    private static Task<int> RunJobAsync(DeliveryHarness harness)
        => harness.InScopeAsync(sp => sp.GetRequiredService<SlotGenerationJob>().RunAsync(Ct));

    private static async Task DispatchOutboxAsync(DeliveryHarness harness)
    {
        var dispatcher = harness.Services.GetRequiredService<OutboxDispatcher>();
        while (await dispatcher.DispatchBatchAsync(Ct) > 0)
        {
        }
    }
}
