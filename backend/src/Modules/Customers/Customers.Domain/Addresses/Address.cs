using System.Text.RegularExpressions;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Customers.Domain.Addresses;

/// <summary>Stored as HOME / WORK / OTHER.</summary>
public enum AddressLabel
{
    Home,
    Work,
    Other,
}

public sealed record AddressData(
    AddressLabel Label,
    string RecipientName,
    string Phone,
    string Line1,
    string? Line2,
    string? Landmark,
    string Locality,
    string CityName,
    string StateCode,
    string Pincode,
    decimal? Latitude,
    decimal? Longitude);

/// <summary>A delivery address (CUS-R3–R5). Never hard-deleted.</summary>
public sealed partial class Address : AggregateRoot
{
    private Address()
    {
        RecipientName = Phone = Line1 = Locality = CityName = StateCode = Pincode = string.Empty;
    }

    public Guid CustomerId { get; private set; }

    public AddressLabel Label { get; private set; }

    public string RecipientName { get; private set; }

    public string Phone { get; private set; }

    public string Line1 { get; private set; }

    public string? Line2 { get; private set; }

    public string? Landmark { get; private set; }

    public string Locality { get; private set; }

    public string CityName { get; private set; }

    public string StateCode { get; private set; }

    public string Pincode { get; private set; }

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public static Address Create(Guid customerId, AddressData data, DateTimeOffset now)
    {
        var address = new Address { CustomerId = customerId, IsActive = true, AddedAt = now };
        address.Replace(data);
        return address;
    }

    public void Replace(AddressData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        EnsureActive();
        var errors = Validate(data);
        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        Label = data.Label;
        RecipientName = data.RecipientName.Trim();
        Phone = data.Phone.Trim();
        Line1 = data.Line1.Trim();
        Line2 = Blank(data.Line2);
        Landmark = Blank(data.Landmark);
        Locality = data.Locality.Trim();
        CityName = data.CityName.Trim();
        StateCode = data.StateCode.Trim().ToUpperInvariant();
        Pincode = data.Pincode.Trim();
        Latitude = data.Latitude;
        Longitude = data.Longitude;
    }

    public void MakeDefault() => IsDefault = true;

    public void ClearDefault() => IsDefault = false;

    public void Deactivate()
    {
        IsActive = false;
        IsDefault = false;
    }

    /// <summary>CUS-R7: personal fields replaced, address inactive.</summary>
    public void Anonymise()
    {
        Deactivate();
        RecipientName = "Deleted";
        Phone = "+910000000000";
        Line1 = "Deleted";
        Line2 = null;
        Landmark = null;
        Latitude = null;
        Longitude = null;
    }

    public static List<FieldError> Validate(AddressData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var errors = new List<FieldError>();
        Required(errors, "recipientName", data.RecipientName, 100);
        Required(errors, "line1", data.Line1, 200);
        Required(errors, "locality", data.Locality, 100);
        Required(errors, "cityName", data.CityName, 100);
        Optional(errors, "line2", data.Line2, 200);
        Optional(errors, "landmark", data.Landmark, 200);
        if (data.Phone is null || !PhonePattern().IsMatch(data.Phone.Trim()))
        {
            errors.Add(new FieldError("phone", "PHONE_INVALID", "The phone must be an E.164 number such as +919876543210."));
        }

        if (data.StateCode is null || !StatePattern().IsMatch(data.StateCode.Trim().ToUpperInvariant()))
        {
            errors.Add(new FieldError("stateCode", "STATE_CODE_INVALID", "The stateCode must be a 2-letter state code such as TN."));
        }

        if (data.Pincode is null || !PincodePattern().IsMatch(data.Pincode.Trim()))
        {
            errors.Add(new FieldError("pincode", "PINCODE_INVALID", "The pincode must be 6 digits."));
        }

        if (data.Latitude is < -90 or > 90)
        {
            errors.Add(new FieldError("latitude", "OUT_OF_RANGE", "The latitude must be between -90 and 90."));
        }

        if (data.Longitude is < -180 or > 180)
        {
            errors.Add(new FieldError("longitude", "OUT_OF_RANGE", "The longitude must be between -180 and 180."));
        }

        if ((data.Latitude is null) != (data.Longitude is null))
        {
            errors.Add(new FieldError("latitude", "COORDINATES_INCOMPLETE", "Send both latitude and longitude, or neither."));
        }

        return errors;
    }

    private void EnsureActive()
    {
        if (!IsActive && CustomerId != Guid.Empty)
        {
            throw new ConflictException(CustomersErrors.InvalidStatusTransition, "This address was deleted.");
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Required(List<FieldError> errors, string field, string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
        {
            errors.Add(new FieldError(field, "VALUE_INVALID", $"The {field} must be 1 to {max} characters."));
        }
    }

    private static void Optional(List<FieldError> errors, string field, string? value, int max)
    {
        if (value is not null && value.Trim().Length > max)
        {
            errors.Add(new FieldError(field, "VALUE_INVALID", $"The {field} must be at most {max} characters."));
        }
    }

    [GeneratedRegex(@"^\+[1-9][0-9]{7,14}$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex StatePattern();

    [GeneratedRegex("^[1-9][0-9]{5}$")]
    private static partial Regex PincodePattern();
}
