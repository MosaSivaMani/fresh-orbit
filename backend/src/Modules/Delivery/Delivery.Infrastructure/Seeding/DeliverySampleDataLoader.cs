using FluentValidation;
using FreshOrbit.Delivery.Application.Riders;
using FreshOrbit.Delivery.Application.Slots;
using FreshOrbit.Delivery.Application.Templates;
using FreshOrbit.Delivery.Domain.Riders;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Domain.Templates;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Delivery.Infrastructure.Seeding;

/// <summary>
/// Sample <c>delivery.json</c> (order 60) through <see cref="SlotTemplateService"/> and <see cref="RiderService"/>.
/// A template is skipped when its location already has one on that day at that start time; a rider when the phone exists.
/// </summary>
public sealed partial class DeliverySampleDataLoader(
    SlotTemplateService templates,
    RiderService riders,
    IValidator<CreateSlotTemplateRequest> templateValidator,
    IValidator<CreateRiderRequest> riderValidator,
    ILocationsApi locations,
    FreshOrbitDbContext db,
    ILogger<DeliverySampleDataLoader> logger) : ISampleDataLoader
{
    private const string FileName = "delivery.json";

    public int Order => 60;

    public async Task LoadAsync(SampleDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var file = await SampleFiles.ReadAsync<DeliveryFile>(context, FileName, cancellationToken);
        var codes = file.SlotTemplates.Select(t => t.LocationCode).Concat(file.Riders.Select(r => r.LocationCode)).Distinct().ToList();
        var locationIds = (await locations.GetFulfillmentLocationsByCodesAsync(codes, cancellationToken)).ToDictionary(l => l.Code, l => l.Id, StringComparer.Ordinal);
        Guid Location(string code) => locationIds.TryGetValue(code, out var id) ? id : throw SampleFiles.Missing(FileName, "fulfillment location", code);
        var ids = locationIds.Values.ToList();

        var existing = (await db.Set<SlotTemplate>().AsNoTracking().Where(t => ids.Contains(t.FulfillmentLocationId)).Select(t => new { t.FulfillmentLocationId, t.DayOfWeek, t.StartTime }).ToListAsync(cancellationToken))
            .Select(t => (t.FulfillmentLocationId, t.DayOfWeek, TimeFormat.Format(t.StartTime))).ToHashSet();
        var insertedTemplates = 0;
        foreach (var row in file.SlotTemplates)
        {
            var locationId = Location(row.LocationCode);
            if (existing.Contains((locationId, row.DayOfWeek, row.StartTime)))
            {
                continue;
            }

            var request = new CreateSlotTemplateRequest(locationId, row.DayOfWeek, row.StartTime, row.EndTime, row.Capacity, row.CutoffMinutes);
            await templateValidator.ValidateAndThrowAsync(request, cancellationToken);
            var template = await templates.CreateAsync(request, cancellationToken);
            context.Track<SlotTemplate>(db, template.Id);
            insertedTemplates++;
        }

        var phones = file.Riders.Select(r => r.Phone).ToList();
        var existingPhones = (await db.Set<Rider>().AsNoTracking().Where(r => phones.Contains(r.Phone)).Select(r => r.Phone).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var insertedRiders = 0;
        foreach (var row in file.Riders.Where(r => !existingPhones.Contains(r.Phone)))
        {
            var request = new CreateRiderRequest(Location(row.LocationCode), row.FullName, row.Phone);
            await riderValidator.ValidateAndThrowAsync(request, cancellationToken);
            var rider = await riders.CreateAsync(request, cancellationToken);
            context.Track<Rider>(db, rider.Id);
            insertedRiders++;
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded(insertedTemplates, insertedRiders);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample delivery inserted: {Templates} slot templates, {Riders} riders (existing keys skipped)")]
    private partial void LogLoaded(int templates, int riders);

    internal sealed record DeliveryFile(IReadOnlyList<TemplateRow> SlotTemplates, IReadOnlyList<RiderRow> Riders);

    internal sealed record TemplateRow(string LocationCode, int DayOfWeek, string StartTime, string EndTime, int Capacity, int CutoffMinutes);

    internal sealed record RiderRow(string LocationCode, string FullName, string Phone);
}

/// <summary>
/// Order 95, after the locations are active: generates the slots of the next <see cref="SlotSchedule.HorizonDays"/>
/// days (DEL-R9, the same service the hourly job uses) so the sample store can take an order today. Slots are not
/// tracked; <see cref="DeliverySamplePurgeParticipant"/> removes the unbooked ones of sample templates (D55).
/// </summary>
public sealed partial class DeliverySlotsSampleDataLoader(
    SlotGenerationService generation,
    ILocationsApi locations,
    FreshOrbitDbContext db,
    ILogger<DeliverySlotsSampleDataLoader> logger) : ISampleDataLoader
{
    public int Order => 95;

    public async Task LoadAsync(SampleDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var file = await SampleFiles.ReadAsync<DeliverySampleDataLoader.DeliveryFile>(context, "delivery.json", cancellationToken);
        var codes = file.SlotTemplates.Select(t => t.LocationCode).Distinct().ToList();
        var added = 0;
        foreach (var location in await locations.GetFulfillmentLocationsByCodesAsync(codes, cancellationToken))
        {
            added += await generation.TrackMissingSlotsAsync(location.Id, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        LogGenerated(added);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample delivery slots generated: {Count}")]
    private partial void LogGenerated(int count);
}

/// <summary>D55: booked slots of sample templates block the purge; unbooked ones are deleted before the tracked rows.</summary>
public sealed class DeliverySamplePurgeParticipant(FreshOrbitDbContext db) : ISamplePurgeParticipant
{
    public async Task<IReadOnlyList<string>> FindBlockersAsync(IReadOnlyDictionary<string, Guid[]> records, CancellationToken cancellationToken)
    {
        var templateIds = records.IdsOf(SampleTracking.TableOf<SlotTemplate>(db));
        if (templateIds.Count == 0)
        {
            return [];
        }

        var booked = await db.Set<DeliverySlot>().AsNoTracking().CountAsync(s => s.SlotTemplateId != null && templateIds.Contains(s.SlotTemplateId.Value) && s.BookedCount > 0, cancellationToken);
        return booked == 0 ? [] : [$"delivery.delivery_slots: {booked} booked slots come from sample slot templates."];
    }

    public async Task PrepareAsync(IReadOnlyDictionary<string, Guid[]> records, CancellationToken cancellationToken)
    {
        var templateIds = records.IdsOf(SampleTracking.TableOf<SlotTemplate>(db));
        if (templateIds.Count > 0)
        {
            await db.Set<DeliverySlot>().Where(s => s.SlotTemplateId != null && templateIds.Contains(s.SlotTemplateId.Value) && s.BookedCount == 0).ExecuteDeleteAsync(cancellationToken);
        }
    }
}
