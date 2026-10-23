using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Locations.Domain.Shared;

/// <summary>Error codes of the Locations module (docs/api/README.md §2, Locations and Common).</summary>
public static class LocationsErrors
{
    public const string ValidationFailed = RefusalException.DefaultCode;
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string Duplicate = "DUPLICATE";
    public const string LocationRequired = "LOCATION_REQUIRED";
    public const string PincodeNotServiceable = "PINCODE_NOT_SERVICEABLE";
    public const string CityNotReady = "CITY_NOT_READY";
    public const string CityNotActive = "CITY_NOT_ACTIVE";
    public const string CityRetirementBlocked = "CITY_RETIREMENT_BLOCKED";
    public const string PincodeAlreadyAssigned = "PINCODE_ALREADY_ASSIGNED";
    public const string CodeTaken = "CODE_TAKEN";
    public const string LocationAccessDenied = "LOCATION_ACCESS_DENIED";

    public const string CityNotFound = "CITY_NOT_FOUND";
    public const string FulfillmentLocationNotFound = "FULFILLMENT_LOCATION_NOT_FOUND";
    public const string ServiceAreaNotFound = "SERVICE_AREA_NOT_FOUND";
    public const string ClosureNotFound = "CLOSURE_NOT_FOUND";
    public const string PincodeNotFound = "PINCODE_NOT_FOUND";

    /// <summary><c>missing[].code</c> values of CITY_NOT_READY owned by Locations (LOC-R6).</summary>
    public const string FulfillmentLocationMissing = "FULFILLMENT_LOCATION_MISSING";
    public const string ServiceAreaMissing = "SERVICE_AREA_MISSING";
    public const string PincodeMissing = "PINCODE_MISSING";
}
