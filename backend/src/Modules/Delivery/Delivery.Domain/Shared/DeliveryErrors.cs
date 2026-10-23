using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Delivery.Domain.Shared;

/// <summary>Error codes raised by the Delivery module (docs/api §2 Delivery).</summary>
public static class DeliveryErrors
{
    public const string ValidationFailed = RefusalException.DefaultCode;
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string Duplicate = "DUPLICATE";
    public const string LocationAccessDenied = "LOCATION_ACCESS_DENIED";
    public const string LocationRequired = "LOCATION_REQUIRED";
    public const string ConfigurationMissing = "CONFIGURATION_MISSING";
    public const string PincodeNotServiceable = "PINCODE_NOT_SERVICEABLE";

    public const string SlotNotAvailable = "SLOT_NOT_AVAILABLE";
    public const string SlotFull = "SLOT_FULL";
    public const string SlotCutoffPassed = "SLOT_CUTOFF_PASSED";
    public const string SlotTemplateOverlap = "SLOT_TEMPLATE_OVERLAP";
    public const string SlotCapacityBelowBooked = "SLOT_CAPACITY_BELOW_BOOKED";
    public const string SlotTemplateMissing = "SLOT_TEMPLATE_MISSING";

    public const string RiderNotAvailable = "RIDER_NOT_AVAILABLE";
    public const string RiderHasActiveShipments = "RIDER_HAS_ACTIVE_SHIPMENTS";

    public const string ShipmentNotDispatchable = "SHIPMENT_NOT_DISPATCHABLE";
    public const string DeliveryCodeInvalid = "DELIVERY_CODE_INVALID";
    public const string DeliveryCodeLocked = "DELIVERY_CODE_LOCKED";
    public const string CashAlreadyCollected = "CASH_ALREADY_COLLECTED";
    public const string ReattemptNotAllowed = "REATTEMPT_NOT_ALLOWED";

    /// <summary>A refusal for one field (422 VALIDATION_FAILED).</summary>
    public static RefusalException Invalid(string field, string message)
        => new([new FieldError(field, ValidationFailed, message)]);
}

/// <summary>A staff member acted on a fulfillment location outside their scope (HTTP 403 LOCATION_ACCESS_DENIED).</summary>
public sealed class LocationAccessDeniedException : ForbiddenException
{
    public LocationAccessDeniedException()
        : base(DeliveryErrors.LocationAccessDenied, "You may not act on this fulfillment location; ask for access to it.", "fulfillmentLocationId")
    {
    }
}
