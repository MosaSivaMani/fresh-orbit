using FluentValidation;
using FreshOrbit.Inventory.Domain.Shared;

namespace FreshOrbit.Inventory.Application.Stock;

/// <summary>A quantity as sent by clients: <c>{ "value": 500, "unit": "GRAM" }</c>. The unit must be the stock item's unit.</summary>
public sealed record QuantityInput(int Value, string Unit);

public sealed record CreateStockItemRequest(Guid FulfillmentLocationId, Guid VariantId, int LowStockThreshold);

public sealed record SetThresholdRequest(int LowStockThreshold);

public sealed record ReceiveStockRequest(
    Guid FulfillmentLocationId,
    Guid VariantId,
    string BatchCode,
    string? SupplierName,
    string? SourceLandingCenter,
    DateOnly? CaughtOrHarvestedOn,
    QuantityInput ReceivedQuantity,
    long TotalCostPaise,
    DateTimeOffset BestBeforeAt,
    decimal? ReceiptTemperatureC);

public sealed record DiscardBatchRequest(string ReasonCode, string? Notes);

public sealed record RecordWasteRequest(Guid StockItemId, Guid? BatchId, QuantityInput Quantity, string ReasonCode, string? Notes);

/// <param name="Kind">ADJUSTMENT_IN, ADJUSTMENT_OUT or RETURN (RETURN needs <paramref name="BatchId"/>).</param>
public sealed record AdjustStockRequest(Guid StockItemId, string Kind, Guid? BatchId, QuantityInput Quantity, string ReasonCode, string? Notes);

public sealed record CountStockRequest(Guid StockItemId, QuantityInput CountedQuantity, Guid? BatchId, string? Notes);

/// <param name="RecordedAt">Optional; within the last 24 hours, default now.</param>
public sealed record RecordTemperatureRequest(Guid FulfillmentLocationId, string StorageUnit, decimal TemperatureC, DateTimeOffset? RecordedAt);

internal static class ValidationRules
{
    public const int MaxNotesLength = 500;
    public const decimal MinTemperatureC = -40.0m;
    public const decimal MaxTemperatureC = 40.0m;

    public static void QuantityRules(this AbstractValidator<QuantityInput> validator, int minimum)
    {
        validator.RuleFor(x => x.Value).GreaterThanOrEqualTo(minimum).WithErrorCode(InventoryErrors.ValidationFailed)
            .WithMessage(minimum == 0 ? "Quantity must not be negative." : "Quantity must be greater than zero.");
        validator.RuleFor(x => x.Unit).NotEmpty().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Quantity unit is required.");
    }
}

public sealed class PositiveQuantityValidator : AbstractValidator<QuantityInput>
{
    public PositiveQuantityValidator() => this.QuantityRules(1);
}

public sealed class NonNegativeQuantityValidator : AbstractValidator<QuantityInput>
{
    public NonNegativeQuantityValidator() => this.QuantityRules(0);
}

public sealed class CreateStockItemRequestValidator : AbstractValidator<CreateStockItemRequest>
{
    public CreateStockItemRequestValidator()
    {
        RuleFor(x => x.FulfillmentLocationId).NotEmpty().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Fulfillment location is required.");
        RuleFor(x => x.VariantId).NotEmpty().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Variant is required.");
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Low-stock threshold must not be negative.");
    }
}

public sealed class SetThresholdRequestValidator : AbstractValidator<SetThresholdRequest>
{
    public SetThresholdRequestValidator()
        => RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Low-stock threshold must not be negative.");
}

public sealed class ReceiveStockRequestValidator : AbstractValidator<ReceiveStockRequest>
{
    public ReceiveStockRequestValidator()
    {
        RuleFor(x => x.FulfillmentLocationId).NotEmpty().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Fulfillment location is required.");
        RuleFor(x => x.VariantId).NotEmpty().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Variant is required.");
        RuleFor(x => x.BatchCode).NotEmpty().MaximumLength(40).WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Batch code must be 1 to 40 characters.");
        RuleFor(x => x.SupplierName).MaximumLength(120).WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Supplier name must be at most 120 characters.");
        RuleFor(x => x.SourceLandingCenter).MaximumLength(120).WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Source landing center must be at most 120 characters.");
        RuleFor(x => x.ReceivedQuantity).NotNull().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Received quantity is required.")
            .SetValidator(new PositiveQuantityValidator());
        RuleFor(x => x.TotalCostPaise).GreaterThanOrEqualTo(0).WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Total cost must not be negative.");
        RuleFor(x => x.ReceiptTemperatureC).InclusiveBetween(ValidationRules.MinTemperatureC, ValidationRules.MaxTemperatureC)
            .WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Receipt temperature must be between -40.0 and 40.0 °C.");
    }
}

public sealed class DiscardBatchRequestValidator : AbstractValidator<DiscardBatchRequest>
{
    public DiscardBatchRequestValidator()
    {
        RuleFor(x => x.ReasonCode).NotEmpty().WithErrorCode(InventoryErrors.ReasonCodeInvalid).WithMessage("Reason code is required.");
        RuleFor(x => x.Notes).MaximumLength(ValidationRules.MaxNotesLength).WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Notes must be at most 500 characters.");
    }
}

public sealed class RecordWasteRequestValidator : AbstractValidator<RecordWasteRequest>
{
    public RecordWasteRequestValidator()
    {
        RuleFor(x => x.StockItemId).NotEmpty().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Stock item is required.");
        RuleFor(x => x.Quantity).NotNull().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Quantity is required.").SetValidator(new PositiveQuantityValidator());
        RuleFor(x => x.ReasonCode).NotEmpty().WithErrorCode(InventoryErrors.ReasonCodeInvalid).WithMessage("Reason code is required.");
        RuleFor(x => x.Notes).MaximumLength(ValidationRules.MaxNotesLength).WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Notes must be at most 500 characters.");
    }
}

public sealed class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(x => x.StockItemId).NotEmpty().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Stock item is required.");
        RuleFor(x => x.Kind).NotEmpty().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Kind is required.");
        RuleFor(x => x.Quantity).NotNull().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Quantity is required.").SetValidator(new PositiveQuantityValidator());
        RuleFor(x => x.ReasonCode).NotEmpty().WithErrorCode(InventoryErrors.ReasonCodeInvalid).WithMessage("Reason code is required.");
        RuleFor(x => x.Notes).MaximumLength(ValidationRules.MaxNotesLength).WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Notes must be at most 500 characters.");
    }
}

public sealed class CountStockRequestValidator : AbstractValidator<CountStockRequest>
{
    public CountStockRequestValidator()
    {
        RuleFor(x => x.StockItemId).NotEmpty().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Stock item is required.");
        RuleFor(x => x.CountedQuantity).NotNull().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Counted quantity is required.").SetValidator(new NonNegativeQuantityValidator());
        RuleFor(x => x.Notes).MaximumLength(ValidationRules.MaxNotesLength).WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Notes must be at most 500 characters.");
    }
}

public sealed class RecordTemperatureRequestValidator : AbstractValidator<RecordTemperatureRequest>
{
    public RecordTemperatureRequestValidator()
    {
        RuleFor(x => x.FulfillmentLocationId).NotEmpty().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Fulfillment location is required.");
        RuleFor(x => x.StorageUnit).NotEmpty().WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Storage unit is required.");
        RuleFor(x => x.TemperatureC).InclusiveBetween(ValidationRules.MinTemperatureC, ValidationRules.MaxTemperatureC)
            .WithErrorCode(InventoryErrors.ValidationFailed).WithMessage("Temperature must be between -40.0 and 40.0 °C.");
    }
}
