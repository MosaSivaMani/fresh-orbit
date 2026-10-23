using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Settings.Domain.OrderPolicies;

/// <summary>Order rules for one city (or the default when <see cref="CityId"/> is null). Rules SET-R2, SET-R3.</summary>
public sealed class OrderPolicy : AggregateRoot
{
    /// <summary>Order statuses up to which a customer may cancel.</summary>
    public static readonly IReadOnlyList<string> CancellableStatuses = ["PENDING_PAYMENT", "CONFIRMED", "PREPARING", "PACKED"];

    /// <summary>Payment method codes an order policy may enable.</summary>
    public static readonly IReadOnlyList<string> PaymentMethods = ["COD", "UPI_ON_DELIVERY", "ONLINE"];

    private OrderPolicy()
    {
        CancelAllowedUntilStatus = string.Empty;
        EnabledPaymentMethods = [];
    }

    public Guid? CityId { get; private set; }

    public int ReservationMinutes { get; private set; }

    public int MaxOverweightBp { get; private set; }

    public string CancelAllowedUntilStatus { get; private set; }

    public long CodMaxOrderPaise { get; private set; }

    public List<string> EnabledPaymentMethods { get; private set; }

    public int SubstitutionResponseMinutes { get; private set; }

    public int ComplaintWindowHours { get; private set; }

    public int MaxDeliveryAttempts { get; private set; }

    public static OrderPolicy Create(Guid? cityId, OrderPolicyData data)
    {
        var policy = new OrderPolicy { CityId = cityId };
        policy.Update(data);
        return policy;
    }

    public void Update(OrderPolicyData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var errors = Validate(data);
        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        ReservationMinutes = data.ReservationMinutes;
        MaxOverweightBp = data.MaxOverweightBp;
        CancelAllowedUntilStatus = data.CancelAllowedUntilStatus;
        CodMaxOrderPaise = data.CodMaxOrderPaise;
        EnabledPaymentMethods = [.. data.EnabledPaymentMethods.Distinct(StringComparer.Ordinal)];
        SubstitutionResponseMinutes = data.SubstitutionResponseMinutes;
        ComplaintWindowHours = data.ComplaintWindowHours;
        MaxDeliveryAttempts = data.MaxDeliveryAttempts;
    }

    public static List<FieldError> Validate(OrderPolicyData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var errors = new List<FieldError>();
        Range(errors, "reservationMinutes", data.ReservationMinutes, 5, 120);
        Range(errors, "maxOverweightBp", data.MaxOverweightBp, 0, 5000);
        Range(errors, "substitutionResponseMinutes", data.SubstitutionResponseMinutes, 5, 120);
        Range(errors, "complaintWindowHours", data.ComplaintWindowHours, 1, 168);
        Range(errors, "maxDeliveryAttempts", data.MaxDeliveryAttempts, 1, 5);
        if (data.CodMaxOrderPaise < 0)
        {
            errors.Add(new FieldError("codMaxOrderPaise", "OUT_OF_RANGE", "The codMaxOrderPaise must not be negative."));
        }

        if (!CancellableStatuses.Contains(data.CancelAllowedUntilStatus))
        {
            errors.Add(new FieldError("cancelAllowedUntilStatus", "OUT_OF_RANGE", "The cancelAllowedUntilStatus must be PENDING_PAYMENT, CONFIRMED, PREPARING or PACKED."));
        }

        if (data.EnabledPaymentMethods is null || data.EnabledPaymentMethods.Count == 0 || data.EnabledPaymentMethods.Any(m => !PaymentMethods.Contains(m)))
        {
            errors.Add(new FieldError("enabledPaymentMethods", "OUT_OF_RANGE", "The enabledPaymentMethods must list one or more of COD, UPI_ON_DELIVERY, ONLINE."));
        }

        return errors;
    }

    private static void Range(List<FieldError> errors, string field, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            errors.Add(new FieldError(field, "OUT_OF_RANGE", $"The {field} must be between {min} and {max}."));
        }
    }
}

public sealed record OrderPolicyData(
    int ReservationMinutes,
    int MaxOverweightBp,
    string CancelAllowedUntilStatus,
    long CodMaxOrderPaise,
    IReadOnlyList<string> EnabledPaymentMethods,
    int SubstitutionResponseMinutes,
    int ComplaintWindowHours,
    int MaxDeliveryAttempts);
