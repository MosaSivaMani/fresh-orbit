using FluentValidation;
using FreshOrbit.Catalog.Public;
using FreshOrbit.Inventory.Application.Stock;
using FreshOrbit.Inventory.Domain.ColdChain;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Inventory.Infrastructure.Seeding;

/// <summary>
/// Sample <c>inventory.json</c> (order 50) through <see cref="StockCommandService"/>: stock items, receipts (each
/// writes its batch and RECEIPT movement and folds them into on-hand in the same save, rule 6; a warm receipt also
/// writes an out-of-range cold-chain log, INV-R11) and cold-chain readings. Existing stock items and batch codes are
/// skipped; a reading already recorded as sample data is skipped.
/// </summary>
public sealed partial class InventorySampleDataLoader(
    StockCommandService stock,
    IValidator<CreateStockItemRequest> createValidator,
    IValidator<ReceiveStockRequest> receiveValidator,
    IValidator<RecordTemperatureRequest> temperatureValidator,
    ICatalogApi catalog,
    ILocationsApi locations,
    FreshOrbitDbContext db,
    ILogger<InventorySampleDataLoader> logger) : ISampleDataLoader
{
    private const string FileName = "inventory.json";

    public int Order => 50;

    public async Task LoadAsync(SampleDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var file = await SampleFiles.ReadAsync<InventoryFile>(context, FileName, cancellationToken);
        var codes = file.StockItems.Select(s => s.LocationCode).Concat(file.Receipts.Select(r => r.LocationCode)).Concat(file.ColdChainLogs.Select(c => c.LocationCode)).Distinct().ToList();
        var skus = file.StockItems.Select(s => s.Sku).Concat(file.Receipts.Select(r => r.Sku)).Distinct().ToList();
        var locationsByCode = (await locations.GetFulfillmentLocationsByCodesAsync(codes, cancellationToken)).ToDictionary(l => l.Code, StringComparer.Ordinal);
        var variantsBySku = (await catalog.GetVariantsBySkusAsync(skus, cancellationToken)).ToDictionary(v => v.Sku, StringComparer.Ordinal);
        var locationIds = locationsByCode.Values.Select(l => l.Id).ToList();
        FulfillmentLocationSummary Location(string code) => locationsByCode.TryGetValue(code, out var l) ? l : throw SampleFiles.Missing(FileName, "fulfillment location", code);
        Guid Variant(string sku) => variantsBySku.TryGetValue(sku, out var v) ? v.VariantId : throw SampleFiles.Missing(FileName, "variant", sku);

        var logsBefore = await db.Set<ColdChainLog>().AsNoTracking().Where(l => locationIds.Contains(l.FulfillmentLocationId)).Select(l => l.Id).ToListAsync(cancellationToken);

        // Stock items
        var existingItems = (await db.Set<StockItem>().AsNoTracking().Where(s => locationIds.Contains(s.FulfillmentLocationId)).Select(s => new { s.FulfillmentLocationId, s.VariantId }).ToListAsync(cancellationToken))
            .Select(s => (s.FulfillmentLocationId, s.VariantId)).ToHashSet();
        var insertedItems = 0;
        foreach (var row in file.StockItems)
        {
            var key = (Location(row.LocationCode).Id, Variant(row.Sku));
            if (existingItems.Contains(key))
            {
                continue;
            }

            var request = new CreateStockItemRequest(key.Item1, key.Item2, row.LowStockThreshold);
            await createValidator.ValidateAndThrowAsync(request, cancellationToken);
            var item = await stock.CreateStockItemAsync(request, cancellationToken);
            context.Track<StockItem>(db, item.Id);
            existingItems.Add(key);
            insertedItems++;
        }

        // Receipts (batch + RECEIPT movement)
        var existingBatches = (await db.Set<Batch>().AsNoTracking().Where(b => locationIds.Contains(b.FulfillmentLocationId)).Select(b => new { b.FulfillmentLocationId, b.BatchCode }).ToListAsync(cancellationToken))
            .Select(b => (b.FulfillmentLocationId, b.BatchCode)).ToHashSet();
        var insertedReceipts = 0;
        foreach (var row in file.Receipts)
        {
            var location = Location(row.LocationCode);
            if (existingBatches.Contains((location.Id, row.BatchCode)))
            {
                continue;
            }

            var request = new ReceiveStockRequest(
                location.Id,
                Variant(row.Sku),
                row.BatchCode,
                row.SupplierName,
                row.SourceLandingCenter,
                row.CaughtOrHarvestedOnOffsetDays is { } days ? SampleFiles.LocalDate(context.StartedAt, location.TimeZone, days) : null,
                new QuantityInput(row.ReceivedQuantity.Value, row.ReceivedQuantity.Unit),
                row.TotalCostPaise,
                context.StartedAt.AddHours(row.BestBeforeOffsetHours),
                row.ReceiptTemperatureC);
            await receiveValidator.ValidateAndThrowAsync(request, cancellationToken);
            var receipt = await stock.ReceiveAsync(request, cancellationToken);
            context.Track<Batch>(db, receipt.Batch.Id);
            context.Track<StockMovement>(db, receipt.Movement.Id);
            insertedReceipts++;
        }

        // Cold-chain readings
        var trackedLogs = await SampleTracking.TrackedIdsAsync<ColdChainLog>(db, cancellationToken);
        var recorded = (await db.Set<ColdChainLog>().AsNoTracking().Where(l => trackedLogs.Contains(l.Id)).Select(l => new { l.FulfillmentLocationId, l.StorageUnit, l.TemperatureC }).ToListAsync(cancellationToken))
            .Select(l => (l.FulfillmentLocationId, l.StorageUnit, l.TemperatureC)).ToHashSet();
        var insertedReadings = 0;
        foreach (var row in file.ColdChainLogs)
        {
            var location = Location(row.LocationCode);
            if (recorded.Contains((location.Id, row.StorageUnit, row.TemperatureC)))
            {
                continue;
            }

            var request = new RecordTemperatureRequest(location.Id, row.StorageUnit, row.TemperatureC, context.StartedAt.AddHours(row.RecordedAtOffsetHours));
            await temperatureValidator.ValidateAndThrowAsync(request, cancellationToken);
            await stock.RecordTemperatureAsync(request, cancellationToken);
            insertedReadings++;
        }

        // Every cold-chain log written by this run (readings and warm-receipt logs) at the sample locations.
        var newLogs = (await db.Set<ColdChainLog>().AsNoTracking().Where(l => locationIds.Contains(l.FulfillmentLocationId)).Select(l => l.Id).ToListAsync(cancellationToken)).Except(logsBefore).ToList();
        context.Track<ColdChainLog>(db, newLogs);
        await db.SaveChangesAsync(cancellationToken);
        LogLoaded(insertedItems, insertedReceipts, insertedReadings, newLogs.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample inventory inserted: {Items} stock items, {Receipts} receipts, {Readings} readings, {Logs} cold-chain logs (existing keys skipped)")]
    private partial void LogLoaded(int items, int receipts, int readings, int logs);

    private sealed record InventoryFile(IReadOnlyList<StockItemRow> StockItems, IReadOnlyList<ReceiptRow> Receipts, IReadOnlyList<ReadingRow> ColdChainLogs);

    private sealed record StockItemRow(string LocationCode, string Sku, int LowStockThreshold);

    private sealed record ReceiptRow(
        string LocationCode,
        string Sku,
        string BatchCode,
        string? SupplierName,
        string? SourceLandingCenter,
        int? CaughtOrHarvestedOnOffsetDays,
        QuantityRow ReceivedQuantity,
        long TotalCostPaise,
        int BestBeforeOffsetHours,
        decimal? ReceiptTemperatureC);

    private sealed record QuantityRow(int Value, string Unit);

    private sealed record ReadingRow(string LocationCode, string StorageUnit, decimal TemperatureC, int RecordedAtOffsetHours);
}
