using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Domain.Templates;
using FreshOrbit.Delivery.Infrastructure;
using FreshOrbit.Delivery.Tests.Support;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: AssemblyFixture(typeof(DeliveryDatabase))]

namespace FreshOrbit.Delivery.Tests.Support;

/// <summary>One Postgres for the assembly with every module's schema plus the Delivery EXCLUDE constraint.</summary>
public sealed class DeliveryDatabase : PostgresFixture;

/// <summary>
/// The Delivery module wired like the Worker (persistence, outbox dispatcher) against the test database, with
/// <see cref="FakeLocations"/> for Locations and a controllable clock.
/// </summary>
public sealed class DeliveryHarness : IAsyncDisposable
{
    public DeliveryHarness(DeliveryDatabase database, DateTimeOffset now, ICurrentActor? actor = null)
    {
        Database = database;
        Clock = new TestClock(now);
        Services = database.CreateServices(
            services =>
            {
                new DeliveryModule().Register(services, new ConfigurationBuilder().Build());
                services.Replace(ServiceDescriptor.Singleton<ILocationCalendar>(Locations));
                services.Replace(ServiceDescriptor.Singleton<ILocationsApi>(Locations));
            },
            Clock,
            actor);
    }

    public DeliveryDatabase Database { get; }

    public TestClock Clock { get; }

    public FakeLocations Locations { get; } = new();

    public ServiceProvider Services { get; }

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The real current time truncated to whole seconds (Postgres keeps microseconds, .NET ticks are finer).</summary>
    public static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero);
    }

    /// <summary>Runs <paramref name="action"/> in a fresh scope (one DbContext).</summary>
    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public Task InScopeAsync(Func<IServiceProvider, Task> action)
        => InScopeAsync<bool>(async sp =>
        {
            await action(sp);
            return true;
        });

    /// <summary>Runs <paramref name="action"/> in a fresh scope and saves its tracked changes once (like the outbox dispatcher).</summary>
    public Task HandleAsync(Func<IServiceProvider, Task> action)
        => InScopeAsync(async sp =>
        {
            await action(sp);
            await sp.GetRequiredService<FreshOrbitDbContext>().SaveChangesAsync(Ct);
        });

    public async Task<SlotTemplate> AddTemplateAsync(Guid locationId, int dayOfWeek, TimeOnly start, TimeOnly end, int capacity = 10, int cutoffMinutes = 120)
    {
        var template = SlotTemplate.Create(locationId, dayOfWeek, start, end, capacity, cutoffMinutes);
        template.ClearEvents();
        await using var db = Database.CreateDbContext(clock: Clock);
        db.Add(template);
        await db.SaveChangesAsync(Ct);
        return template;
    }

    public async Task<DeliverySlot> AddSlotAsync(Guid locationId, DateTimeOffset startAt, int capacity = 10, int booked = 0, int cutoffMinutes = 120, DateOnly? slotDate = null)
    {
        var slot = DeliverySlot.Create(locationId, null, slotDate ?? DateOnly.FromDateTime(startAt.UtcDateTime), startAt, startAt.AddHours(2), startAt.AddMinutes(-cutoffMinutes), capacity);
        for (var i = 0; i < booked; i++)
        {
            slot.Book(locationId, startAt.AddMinutes(-cutoffMinutes - 1));
        }

        await using var db = Database.CreateDbContext(clock: Clock);
        db.Add(slot);
        await db.SaveChangesAsync(Ct);
        return slot;
    }

    public async Task<T?> FindAsync<T>(Guid id)
        where T : class
    {
        await using var db = Database.CreateDbContext(clock: Clock);
        return await db.Set<T>().FindAsync([id], Ct);
    }

    public async Task<List<DeliverySlot>> SlotsOfAsync(Guid locationId)
    {
        await using var db = Database.CreateDbContext(clock: Clock);
        return await db.Set<DeliverySlot>().AsNoTracking().Where(s => s.FulfillmentLocationId == locationId).OrderBy(s => s.StartAt).ToListAsync(Ct);
    }

    /// <summary>Outbox event type short names (class names) whose payload mentions <paramref name="id"/>.</summary>
    public async Task<List<string>> OutboxEventsForAsync(Guid id)
    {
        await using var db = Database.CreateDbContext(clock: Clock);
        var needle = id.ToString();
        var rows = await db.Database
            .SqlQuery<string>($"SELECT event_type AS \"Value\" FROM platform.outbox WHERE payload::text LIKE {"%" + needle + "%"} ORDER BY occurred_at, id")
            .ToListAsync(Ct);
        return [.. rows.Select(t => t.Split(',')[0].Split('.')[^1])];
    }

    public async ValueTask DisposeAsync() => await Services.DisposeAsync();
}
