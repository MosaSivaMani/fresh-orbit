using FreshOrbit.Catalog.Public;
using FreshOrbit.Inventory.Application.ColdChain;
using FreshOrbit.Inventory.Application.Common;
using FreshOrbit.Inventory.Domain.ColdChain;
using FreshOrbit.Inventory.Domain.Shared;
using FreshOrbit.Inventory.Domain.Stock;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;
using DomainStockUnit = FreshOrbit.Inventory.Domain.Stock.StockUnit;

namespace FreshOrbit.Inventory.Application.Stock;

/// <summary>Staff commands on stock (docs/product/inventory.md §4). Each command saves once.</summary>
public sealed class StockCommandService(
    IStockItemRepository stockItems,
    IColdChainRepository coldChain,
    IInventoryUnitOfWork unitOfWork,
    ICatalogApi catalog,
    ILocationsApi locations,
    ICurrentActor actor,
    IClock clock)
{
    private static readonly TimeSpan TemperatureBackdateLimit = TimeSpan.FromHours(24);

    private Guid? RecordedBy => actor.StaffMemberId ?? actor.UserId;

    public Task<StockItemResponse> CreateStockItemAsync(CreateStockItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        LocationScope.Ensure(actor, request.FulfillmentLocationId);
        return SaveRetry.RunAsync(unitOfWork, async () =>
        {
            var variant = (await catalog.GetVariantsAsync([request.VariantId], cancellationToken)).FirstOrDefault(v => v.VariantId == request.VariantId);
            if (variant is not { IsActive: true })
            {
                throw Invalid("variantId", "The variant does not exist or is not active.");
            }

            var location = (await locations.GetFulfillmentLocationsAsync([request.FulfillmentLocationId], cancellationToken))
                .FirstOrDefault(l => l.Id == request.FulfillmentLocationId)
                ?? throw Invalid("fulfillmentLocationId", "The fulfillment location does not exist.");
            if (await stockItems.ExistsAsync(location.Id, variant.VariantId, cancellationToken))
            {
                throw new ConflictException(InventoryErrors.Duplicate, "A stock item already exists for this variant at this location.", "variantId");
            }

            var unit = variant.StockUnit == Catalog.Public.StockUnit.Piece ? DomainStockUnit.Piece : DomainStockUnit.Gram;
            var item = StockItem.Create(location.Id, variant.VariantId, unit, request.LowStockThreshold);
            stockItems.Add(item);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return item.ToResponse(includeBatches: true);
        });
    }

    public async Task<StockItemResponse> SetThresholdAsync(Guid stockItemId, uint expectedVersion, SetThresholdRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await LoadAsync(stockItemId, null, cancellationToken);
        unitOfWork.SetExpectedVersion(item, expectedVersion);
        item.SetLowStockThreshold(request.LowStockThreshold);
        stockItems.MarkChanged(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.ToResponse(includeBatches: true);
    }

    public Task<ReceiptResponse> ReceiveAsync(ReceiveStockRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        LocationScope.Ensure(actor, request.FulfillmentLocationId);
        return SaveRetry.RunAsync(unitOfWork, async () =>
        {
            var now = clock.UtcNow;
            var item = await stockItems.GetByLocationAndVariantAsync(request.FulfillmentLocationId, request.VariantId, withActiveChildren: true, cancellationToken)
                ?? throw new DomainException(InventoryErrors.StockItemMissing, "There is no stock item for this variant at this location; create it first.", "variantId");
            EnsureUnit(request.ReceivedQuantity, item.StockUnit, "receivedQuantity");
            if (await stockItems.BatchCodeExistsAsync(item.FulfillmentLocationId, request.BatchCode, cancellationToken))
            {
                throw new ConflictException(InventoryErrors.BatchCodeTaken, "This batch code is already used at the location.", "batchCode");
            }

            if (request.CaughtOrHarvestedOn is { } caughtOn && caughtOn > await LocalTodayAsync(item.FulfillmentLocationId, now, cancellationToken))
            {
                throw Invalid("caughtOrHarvestedOn", "Caught or harvested date must not be after today.");
            }

            StorageUnitRange? receiptRange = null;
            var temperatureWarning = false;
            if (request.ReceiptTemperatureC is { } temperature)
            {
                receiptRange = await coldChain.GetRangeAsync(StorageUnitRange.Receipt, cancellationToken)
                    ?? throw new DomainException(InventoryErrors.ConfigurationMissing, "The RECEIPT storage-unit range is not configured.", "receiptTemperatureC");
                temperatureWarning = !receiptRange.Contains(temperature);
            }

            var batch = item.Receive(
                new BatchReceipt(
                    request.BatchCode.Trim(),
                    Trimmed(request.SupplierName),
                    Trimmed(request.SourceLandingCenter),
                    request.CaughtOrHarvestedOn,
                    request.ReceivedQuantity.Value,
                    request.TotalCostPaise,
                    request.BestBeforeAt,
                    request.ReceiptTemperatureC),
                now,
                RecordedBy,
                temperatureWarning);
            if (temperatureWarning)
            {
                coldChain.Add(ColdChainLog.Record(item.FulfillmentLocationId, receiptRange!, request.ReceiptTemperatureC!.Value, now, RecordedBy));
            }

            stockItems.MarkChanged(item);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            var version = StockMappings.FormatVersion(item.Version);
            return new ReceiptResponse(batch.ToResponse(item.StockUnit, version), item.Movements[^1].ToResponse(item.StockUnit), item.ToResponse(includeBatches: true), temperatureWarning);
        });
    }

    public async Task<StockChangeResponse> DiscardBatchAsync(Guid batchId, uint expectedVersion, DiscardBatchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = ParseReason<WasteReason>(request.ReasonCode);
        var item = await stockItems.GetByBatchIdAsync(batchId, cancellationToken) ?? throw new NotFoundException("No batch has this id.");
        LocationScope.Ensure(actor, item.FulfillmentLocationId);
        unitOfWork.SetExpectedVersion(item, expectedVersion);
        var movements = item.DiscardBatch(batchId, reason, Trimmed(request.Notes), clock.UtcNow, RecordedBy);
        stockItems.MarkChanged(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.ToChangeResponse(movements);
    }

    public Task<StockChangeResponse> RecordWasteAsync(RecordWasteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = ParseReason<WasteReason>(request.ReasonCode);
        var referenceId = IdGenerator.NewId();
        return SaveRetry.RunAsync(unitOfWork, async () =>
        {
            var item = await LoadAsync(request.StockItemId, request.BatchId, cancellationToken);
            EnsureUnit(request.Quantity, item.StockUnit, "quantity");
            var movements = item.RecordWaste(reason, Trimmed(request.Notes), request.Quantity.Value, request.BatchId, referenceId, clock.UtcNow, RecordedBy);
            stockItems.MarkChanged(item);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return item.ToChangeResponse(movements);
        });
    }

    public async Task<StockChangeResponse> AdjustAsync(uint expectedVersion, AdjustStockRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumCodes.TryToEnum<AdjustmentKind>(request.Kind, out var kind))
        {
            throw Invalid("kind", "Kind must be ADJUSTMENT_IN, ADJUSTMENT_OUT or RETURN.");
        }

        var reason = ParseReason<AdjustmentReason>(request.ReasonCode);
        var item = await LoadAsync(request.StockItemId, request.BatchId, cancellationToken);
        unitOfWork.SetExpectedVersion(item, expectedVersion);
        EnsureUnit(request.Quantity, item.StockUnit, "quantity");
        var movements = item.Adjust(kind, reason, Trimmed(request.Notes), request.Quantity.Value, request.BatchId, IdGenerator.NewId(), clock.UtcNow, RecordedBy);
        stockItems.MarkChanged(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.ToChangeResponse(movements);
    }

    public async Task<StockChangeResponse> CountAsync(uint expectedVersion, CountStockRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await LoadAsync(request.StockItemId, request.BatchId, cancellationToken);
        unitOfWork.SetExpectedVersion(item, expectedVersion);
        EnsureUnit(request.CountedQuantity, item.StockUnit, "countedQuantity");
        var movements = item.Count(request.CountedQuantity.Value, request.BatchId, Trimmed(request.Notes), IdGenerator.NewId(), clock.UtcNow, RecordedBy);
        if (movements.Count > 0)
        {
            stockItems.MarkChanged(item);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return item.ToChangeResponse(movements);
    }

    public async Task<ColdChainLogResponse> RecordTemperatureAsync(RecordTemperatureRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        LocationScope.Ensure(actor, request.FulfillmentLocationId);
        var now = clock.UtcNow;
        var recordedAt = request.RecordedAt ?? now;
        if (recordedAt > now || recordedAt < now - TemperatureBackdateLimit)
        {
            throw Invalid("recordedAt", "Recorded time must be within the last 24 hours.");
        }

        var location = (await locations.GetFulfillmentLocationsAsync([request.FulfillmentLocationId], cancellationToken)).FirstOrDefault(l => l.Id == request.FulfillmentLocationId)
            ?? throw Invalid("fulfillmentLocationId", "The fulfillment location does not exist.");
        var range = await coldChain.GetRangeAsync(request.StorageUnit, cancellationToken)
            ?? throw Invalid("storageUnit", "The storage unit is not a configured storage unit code.");
        var log = ColdChainLog.Record(location.Id, range, request.TemperatureC, recordedAt, RecordedBy);
        coldChain.Add(log);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return log.ToResponse();
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RefusalException Invalid(string field, string message) => new(new FieldError(field, InventoryErrors.ValidationFailed, message));

    private static TEnum ParseReason<TEnum>(string code)
        where TEnum : struct, Enum
        => EnumCodes.TryToEnum<TEnum>(code, out var reason)
            ? reason
            : throw new DomainException(InventoryErrors.ReasonCodeInvalid, $"Reason code must be one of {string.Join(", ", EnumCodes.AllCodes<TEnum>())}.", "reasonCode");

    private static void EnsureUnit(QuantityInput quantity, DomainStockUnit unit, string field)
    {
        if (!EnumCodes.TryToEnum<DomainStockUnit>(quantity.Unit, out var given) || given != unit)
        {
            throw Invalid($"{field}.unit", $"Quantity unit must be {unit.ToCode()} for this stock item.");
        }
    }

    private async Task<StockItem> LoadAsync(Guid stockItemId, Guid? batchId, CancellationToken cancellationToken)
    {
        var item = await stockItems.GetWithActiveChildrenAsync(stockItemId, batchId, cancellationToken)
            ?? throw new NotFoundException("No stock item has this id.");
        LocationScope.Ensure(actor, item.FulfillmentLocationId);
        return item;
    }

    private async Task<DateOnly> LocalTodayAsync(Guid fulfillmentLocationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var location = (await locations.GetFulfillmentLocationsAsync([fulfillmentLocationId], cancellationToken)).FirstOrDefault(l => l.Id == fulfillmentLocationId)
            ?? throw Invalid("fulfillmentLocationId", "The fulfillment location does not exist.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }
}
