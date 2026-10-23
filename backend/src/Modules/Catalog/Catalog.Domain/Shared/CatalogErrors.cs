using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Catalog.Domain.Shared;

/// <summary>Error codes of the Catalog module (docs/api/README.md §2, Catalog and Common).</summary>
public static class CatalogErrors
{
    public const string ValidationFailed = RefusalException.DefaultCode;
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string LocationRequired = "LOCATION_REQUIRED";
    public const string PincodeNotServiceable = "PINCODE_NOT_SERVICEABLE";
    public const string ProductNotAvailable = "PRODUCT_NOT_AVAILABLE";
    public const string PreparationNotAvailable = "PREPARATION_NOT_AVAILABLE";
    public const string QuantityBelowMin = "QUANTITY_BELOW_MIN";
    public const string QuantityAboveMax = "QUANTITY_ABOVE_MAX";
    public const string QuantityStepInvalid = "QUANTITY_STEP_INVALID";
    public const string QuantityUnitInvalid = "QUANTITY_UNIT_INVALID";
    public const string SlugTaken = "SLUG_TAKEN";
    public const string SkuTaken = "SKU_TAKEN";
    public const string DefaultPreparationRequired = "DEFAULT_PREPARATION_REQUIRED";
    public const string YieldInvalid = "YIELD_INVALID";
    public const string ImageInvalid = "IMAGE_INVALID";

    public const string CategoryNotFound = "CATEGORY_NOT_FOUND";
    public const string ProductNotFound = "PRODUCT_NOT_FOUND";
    public const string VariantNotFound = "VARIANT_NOT_FOUND";
    public const string ImageNotFound = "IMAGE_NOT_FOUND";
}
