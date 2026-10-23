namespace FreshOrbit.Cart.Domain.Shared;

/// <summary>Error and issue codes used by the Cart module (docs/api §2).</summary>
public static class CartErrors
{
    public const string CartNotFound = "CART_NOT_FOUND";
    public const string CartItemNotFound = "CART_ITEM_NOT_FOUND";
    public const string CartEmpty = "CART_EMPTY";
    public const string CartItemLimit = "CART_ITEM_LIMIT";
    public const string CartLocationChanged = "CART_LOCATION_CHANGED";
    public const string LocationRequired = "LOCATION_REQUIRED";
    public const string LoginRequired = "LOGIN_REQUIRED";
    public const string PincodeNotServiceable = "PINCODE_NOT_SERVICEABLE";
    public const string ProductNotAvailable = "PRODUCT_NOT_AVAILABLE";
    public const string PreparationNotAvailable = "PREPARATION_NOT_AVAILABLE";
    public const string PreparationChargeNotAvailable = "PREPARATION_CHARGE_NOT_AVAILABLE";
    public const string PriceNotAvailable = "PRICE_NOT_AVAILABLE";
    public const string TaxRuleMissing = "TAX_RULE_MISSING";
    public const string StockItemMissing = "STOCK_ITEM_MISSING";
    public const string OutOfStock = "OUT_OF_STOCK";
    public const string QuantityBelowMin = "QUANTITY_BELOW_MIN";
    public const string QuantityAboveMax = "QUANTITY_ABOVE_MAX";
    public const string QuantityStepInvalid = "QUANTITY_STEP_INVALID";
    public const string QuantityUnitInvalid = "QUANTITY_UNIT_INVALID";
    public const string MinOrderNotMet = "MIN_ORDER_NOT_MET";
    public const string ConfigurationMissing = "CONFIGURATION_MISSING";
}
