using AwesomeAssertions;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Public;
using FreshOrbit.Delivery.Tests.Support;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Delivery.Tests.Integration;

public sealed class SlotBookingTests(DeliveryDatabase database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task I1_concurrent_bookings_for_the_last_capacity_exactly_one_wins()
    {
        var now = DeliveryHarness.Now();
        await using var harness = new DeliveryHarness(database, now);
        var location = harness.Locations.AddLocation();
        var slot = await harness.AddSlotAsync(location.Id, now.AddHours(6), capacity: 1);
        var bothRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readers = 0;

        async Task<string> CheckoutAsync()
        {
            for (var attempt = 1; ; attempt++)
            {
                await using var scope = harness.Services.CreateAsyncScope();
                var api = scope.ServiceProvider.GetRequiredService<IDeliveryApi>();
                var booking = await api.BookSlotAsync(slot.Id, location.Id, Ct);
                if (attempt == 1 && Interlocked.Increment(ref readers) == 2)
                {
                    bothRead.SetResult();
                }

                if (attempt == 1)
                {
                    await bothRead.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
                }

                if (!booking.Succeeded)
                {
                    return booking.RefusalCode!;
                }

                try
                {
                    await scope.ServiceProvider.GetRequiredService<FreshOrbitDbContext>().SaveChangesAsync(Ct);
                    return "BOOKED";
                }
                catch (ConcurrentSaveException) when (attempt < 3)
                {
                    // Lost the race: retry the whole checkout from Read.
                }
            }
        }

        var results = await Task.WhenAll(Task.Run(CheckoutAsync, Ct), Task.Run(CheckoutAsync, Ct));

        results.Should().BeEquivalentTo(["BOOKED", "SLOT_FULL"]);
        (await harness.FindAsync<DeliverySlot>(slot.Id))!.BookedCount.Should().Be(1);
    }

    [Fact]
    public async Task I2_booking_after_cutoff_is_refused_and_nothing_is_saved()
    {
        var now = DeliveryHarness.Now();
        await using var harness = new DeliveryHarness(database, now);
        var location = harness.Locations.AddLocation();
        var slot = await harness.AddSlotAsync(location.Id, now.AddHours(3), capacity: 5);
        harness.Clock.Set(slot.CutoffAt);

        var booking = await harness.InScopeAsync(async sp =>
        {
            var result = await sp.GetRequiredService<IDeliveryApi>().BookSlotAsync(slot.Id, location.Id, Ct);
            await sp.GetRequiredService<FreshOrbitDbContext>().SaveChangesAsync(Ct);
            return result;
        });

        booking.Succeeded.Should().BeFalse();
        booking.RefusalCode.Should().Be("SLOT_CUTOFF_PASSED");
        (await harness.FindAsync<DeliverySlot>(slot.Id))!.BookedCount.Should().Be(0);
    }

    [Fact]
    public async Task Booking_another_locations_slot_or_unknown_slot_is_not_available()
    {
        var now = DeliveryHarness.Now();
        await using var harness = new DeliveryHarness(database, now);
        var location = harness.Locations.AddLocation();
        var slot = await harness.AddSlotAsync(location.Id, now.AddHours(6));

        var results = await harness.InScopeAsync(async sp =>
        {
            var api = sp.GetRequiredService<IDeliveryApi>();
            return new[]
            {
                await api.BookSlotAsync(slot.Id, Guid.CreateVersion7(), Ct),
                await api.BookSlotAsync(Guid.CreateVersion7(), location.Id, Ct),
            };
        });

        results.Select(r => r.RefusalCode).Should().Equal("SLOT_NOT_AVAILABLE", "SLOT_NOT_AVAILABLE");
    }

    [Fact]
    public async Task Public_reads_open_slots_next_slot_and_slot_view()
    {
        var now = DeliveryHarness.Now();
        await using var harness = new DeliveryHarness(database, now);
        var location = harness.Locations.AddLocation();
        var full = await harness.AddSlotAsync(location.Id, now.AddHours(4), capacity: 1, booked: 1);
        var open = await harness.AddSlotAsync(location.Id, now.AddHours(6), capacity: 3, booked: 1);

        var (slots, next, view) = await harness.InScopeAsync(async sp =>
        {
            var api = sp.GetRequiredService<IDeliveryApi>();
            return (
                await api.GetOpenSlotsAsync(location.Id, now, now.AddDays(1), Ct),
                await sp.GetRequiredService<INextSlotProvider>().GetNextOpenSlotAsync(location.Id, Ct),
                await api.GetSlotAsync(full.Id, location.Id, Ct));
        });

        slots.Should().ContainSingle().Which.Should().Be(new SlotInfo(open.Id, open.StartAt, open.EndAt, open.CutoffAt, 2));
        next!.SlotId.Should().Be(open.Id);
        view!.Status.Should().Be("FULL");
    }
}
