using FreshOrbit.Locations.Public.Events;
using FreshOrbit.Locations.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Locations.Domain.Cities;

/// <summary>Stored as DRAFT / ACTIVE / PAUSED / RETIRED.</summary>
public enum CityStatus
{
    Draft,
    Active,
    Paused,
    Retired,
}

/// <summary>A city the business sells in. Activation needs every readiness item (LOC-R6).</summary>
public sealed class City : AggregateRoot
{
    private City()
    {
        Code = Name = StateCode = TimeZone = string.Empty;
    }

    private City(string code, string name, string stateCode, string timeZone)
    {
        Code = code;
        Name = name;
        StateCode = stateCode;
        TimeZone = timeZone;
        Status = CityStatus.Draft;
    }

    /// <summary>UPPER_SNAKE, immutable (LOC-R10).</summary>
    public string Code { get; private set; }

    public string Name { get; private set; }

    public string StateCode { get; private set; }

    /// <summary>Display name of the state ("Andhra Pradesh"); optional configured data, never derived from the code.</summary>
    public string? StateName { get; private set; }

    /// <summary>IANA time zone; dates and times of day of the city's locations are in it (LOC-R17).</summary>
    public string TimeZone { get; private set; }

    public CityStatus Status { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    /// <summary>User id of the staff member who last activated the city.</summary>
    public Guid? ActivatedBy { get; private set; }

    /// <param name="stateName">Optional display name of the state; blank → none.</param>
    public static City Create(string? code, string? name, string? stateCode, string? timeZone, string? stateName = null)
    {
        var errors = new List<FieldError>();
        if (!Rules.IsCityCode(code))
        {
            errors.Add(new FieldError("code", LocationsErrors.ValidationFailed, "Enter the code in UPPER_SNAKE (letters, digits and underscores), at most 40 characters."));
        }

        Validate(errors, name, stateCode, timeZone, stateName);
        Rules.ThrowIfAny(errors);
        return new City(code!, name!.Trim(), stateCode!, timeZone!) { StateName = Rules.TrimToNull(stateName) };
    }

    /// <param name="stateName">Optional display name of the state; null or blank clears it.</param>
    public void Update(string? name, string? stateCode, string? timeZone, string? stateName = null)
    {
        var errors = new List<FieldError>();
        Validate(errors, name, stateCode, timeZone, stateName);
        Rules.ThrowIfAny(errors);
        Name = name!.Trim();
        StateCode = stateCode!;
        StateName = Rules.TrimToNull(stateName);
        TimeZone = timeZone!;
    }

    /// <summary>DRAFT/PAUSED → ACTIVE when <paramref name="missing"/> is empty (LOC-R6); else every item is reported.</summary>
    public void Activate(IReadOnlyList<MissingItem> missing, DateTimeOffset at, Guid? by)
    {
        ArgumentNullException.ThrowIfNull(missing);
        if (Status is not (CityStatus.Draft or CityStatus.Paused))
        {
            throw Transition("activated");
        }

        if (missing.Count > 0)
        {
            throw new CityNotReadyException(Name, missing);
        }

        Status = CityStatus.Active;
        ActivatedAt = at;
        ActivatedBy = by;
        Raise(new CityActivated(Id, Code, at, by));
    }

    /// <summary>ACTIVE → PAUSED (LOC-R8): no new carts or orders; placed orders continue.</summary>
    public void Pause()
    {
        if (Status != CityStatus.Active)
        {
            throw Transition("paused");
        }

        Status = CityStatus.Paused;
        Raise(new CityPaused(Id));
    }

    /// <summary>DRAFT/PAUSED → RETIRED when nothing blocks it (LOC-R9). ACTIVE must be paused first.</summary>
    public void Retire(IReadOnlyList<string> blocking)
    {
        ArgumentNullException.ThrowIfNull(blocking);
        if (Status is not (CityStatus.Draft or CityStatus.Paused))
        {
            throw Transition(Status == CityStatus.Active ? "retired before it is paused" : "retired");
        }

        if (blocking.Count > 0)
        {
            throw new CityRetirementBlockedException(blocking);
        }

        Status = CityStatus.Retired;
        Raise(new CityRetired(Id));
    }

    private static void Validate(List<FieldError> errors, string? name, string? stateCode, string? timeZone, string? stateName)
    {
        Rules.RequireName(errors, "name", name);
        if (!Rules.IsStateCode(stateCode))
        {
            errors.Add(new FieldError("stateCode", LocationsErrors.ValidationFailed, "Enter the two-letter state code, for example TN."));
        }

        if (stateName is not null && stateName.Trim().Length > Rules.StateNameMaxLength)
        {
            errors.Add(new FieldError("stateName", LocationsErrors.ValidationFailed, $"Enter a stateName of at most {Rules.StateNameMaxLength} characters, or leave it empty."));
        }

        if (!Rules.IsTimeZone(timeZone))
        {
            errors.Add(new FieldError("timeZone", LocationsErrors.ValidationFailed, "Enter an IANA time zone, for example Asia/Kolkata."));
        }
    }

    private ConflictException Transition(string action)
        => new(LocationsErrors.InvalidStatusTransition, $"A {Status.ToString().ToUpperInvariant()} city cannot be {action}.", "status");
}
