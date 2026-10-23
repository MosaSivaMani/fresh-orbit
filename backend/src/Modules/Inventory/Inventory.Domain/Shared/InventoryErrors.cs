namespace FreshOrbit.Inventory.Domain.Shared;

/// <summary>Error codes of the Inventory module (docs/api §2 Inventory and the common catalogue).</summary>
public static class InventoryErrors
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Duplicate = "DUPLICATE";
    public const string NotFound = "NOT_FOUND";
    public const string OutOfStock = "OUT_OF_STOCK";
    public const string StockItemMissing = "STOCK_ITEM_MISSING";
    public const string BatchCodeTaken = "BATCH_CODE_TAKEN";
    public const string BatchNotActive = "BATCH_NOT_ACTIVE";
    public const string AdjustmentBelowZero = "ADJUSTMENT_BELOW_ZERO";
    public const string ReasonCodeInvalid = "REASON_CODE_INVALID";
    public const string LocationAccessDenied = "LOCATION_ACCESS_DENIED";
    public const string ConfigurationMissing = "CONFIGURATION_MISSING";
}
