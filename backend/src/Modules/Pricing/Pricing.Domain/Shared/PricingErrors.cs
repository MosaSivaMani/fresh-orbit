using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Pricing.Domain.Shared;

/// <summary>Error codes of the Pricing module (docs/api/README.md §2, Pricing and Common).</summary>
public static class PricingErrors
{
    public const string ValidationFailed = RefusalException.DefaultCode;
    public const string PriceNotAvailable = "PRICE_NOT_AVAILABLE";
    public const string PreparationChargeNotAvailable = "PREPARATION_CHARGE_NOT_AVAILABLE";
    public const string TaxRuleMissing = "TAX_RULE_MISSING";
    public const string PricePeriodOverlap = "PRICE_PERIOD_OVERLAP";
    public const string PriceInPast = "PRICE_IN_PAST";
    public const string ProductNotAvailable = "PRODUCT_NOT_AVAILABLE";
    public const string PreparationNotAvailable = "PREPARATION_NOT_AVAILABLE";
    public const string PriceMissing = "PRICE_MISSING";
    public const string VariantNotFound = "VARIANT_NOT_FOUND";
    public const string FulfillmentLocationNotFound = "FULFILLMENT_LOCATION_NOT_FOUND";
    public const string LocationAccessDenied = "LOCATION_ACCESS_DENIED";
    public const string VersionRequired = "VERSION_REQUIRED";
}
