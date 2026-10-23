using FreshOrbit.Locations.Public.Events;
using FreshOrbit.Locations.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Locations.Domain.FulfillmentLocations;

/// <summary>Stored as DRAFT / ACTIVE / PAUSED.</summary>
public enum LocationStatus
{
    Draft,
    Active,
    Paused,
}

/// <summary>The editable details of a fulfillment location.</summary>
public sealed record LocationDetails(
    string? Name,
    string? AddressLine,
    string? Pincode,
    string? Phone,
    decimal? Latitude,
    decimal? Longitude,
    bool HasColdStorage,
    string? FssaiLicenseNo);

/// <summary>A store / dark store that packs and dispatches orders. Children: weekly hours and closures.</summary>
public sealed class FulfillmentLocation : AggregateRoot
{
    public const int AddressMaxLength = 300;
    public const int PhoneMaxLength = 20;
    public const int FssaiMaxLength = 20;

    private readonly List<OperatingHours> _operatingHours = [];
    private readonly List<LocationClosure> _closures = [];

    private FulfillmentLocation()
    {
        Code = Name = string.Empty;
    }

    private FulfillmentLocation(Guid cityId, string code, LocationDetails details)
    {
        CityId = cityId;
        Code = code;
        Name = string.Empty;
        Apply(details);
        Status = LocationStatus.Draft;
    }

    public Guid CityId { get; private set; }

    /// <summary>UPPER_SNAKE / hyphen, immutable (LOC-R15).</summary>
    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? AddressLine { get; private set; }

    public string? Pincode { get; private set; }

    public string? Phone { get; private set; }

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public bool HasColdStorage { get; private set; }

    public string? FssaiLicenseNo { get; private set; }

    public LocationStatus Status { get; private set; }

    public IReadOnlyList<OperatingHours> OperatingHours => _operatingHours;

    public IReadOnlyList<LocationClosure> Closures => _closures;

    /// <summary>A DRAFT location in a city that is not RETIRED.</summary>
    public static FulfillmentLocation Create(Guid cityId, bool cityRetired, string? code, LocationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = new List<FieldError>();
        if (cityRetired)
        {
            errors.Add(new FieldError("cityId", LocationsErrors.ValidationFailed, "Choose a city that is not retired."));
        }

        if (!Rules.IsLocationCode(code))
        {
            errors.Add(new FieldError("code", LocationsErrors.ValidationFailed, "Enter the code in upper case, digits, '-' or '_' (for example CHN-TVM), at most 40 characters."));
        }

        ValidateDetails(errors, details);
        Rules.ThrowIfAny(errors);
        return new FulfillmentLocation(cityId, code!, details);
    }

    public void Update(LocationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = new List<FieldError>();
        ValidateDetails(errors, details);
        Rules.ThrowIfAny(errors);
        Apply(details);
        if (Status == LocationStatus.Active)
        {
            // An ACTIVE location must keep meeting LOC-R11.
            Rules.ThrowIfAny(ActivationErrors(cityRetired: false));
        }
    }

    /// <summary>
    /// Status change (LOC-R11, LOC-R12). <paramref name="otherActiveLocationsInCity"/> counts ACTIVE siblings;
    /// pausing the last ACTIVE location of an ACTIVE city is refused.
    /// </summary>
    public void ChangeStatus(LocationStatus target, bool cityActive, bool cityRetired, int otherActiveLocationsInCity)
    {
        if (target == Status)
        {
            return;
        }

        switch (Status, target)
        {
            case (LocationStatus.Draft, LocationStatus.Active):
            case (LocationStatus.Paused, LocationStatus.Active):
                Rules.ThrowIfAny(ActivationErrors(cityRetired));
                break;
            case (LocationStatus.Active, LocationStatus.Paused):
                if (cityActive && otherActiveLocationsInCity == 0)
                {
                    throw new ConflictException(LocationsErrors.InvalidStatusTransition, "The last active location of an active city cannot be paused; pause the city first.", "status");
                }

                break;
            default:
                throw new ConflictException(LocationsErrors.InvalidStatusTransition, $"A {Status.ToString().ToUpperInvariant()} location cannot become {target.ToString().ToUpperInvariant()}.", "status");
        }

        var from = Status;
        Status = target;
        Raise(new FulfillmentLocationStatusChanged(Id, from.ToString().ToUpperInvariant(), target.ToString().ToUpperInvariant()));
    }

    /// <summary>LOC-R13: replaces the whole week; a day without an entry is closed.</summary>
    public void SetOperatingHours(IReadOnlyList<(DayOfWeek Day, TimeOnly OpensAt, TimeOnly ClosesAt)> week)
    {
        ArgumentNullException.ThrowIfNull(week);
        var errors = new List<FieldError>();
        var seen = new HashSet<DayOfWeek>();
        for (var i = 0; i < week.Count; i++)
        {
            var (day, opensAt, closesAt) = week[i];
            if (!Enum.IsDefined(day) || !seen.Add(day))
            {
                errors.Add(new FieldError($"days[{i}].dayOfWeek", LocationsErrors.ValidationFailed, "Enter each day of the week at most once."));
            }

            if (opensAt >= closesAt)
            {
                errors.Add(new FieldError($"days[{i}].closesAt", LocationsErrors.ValidationFailed, "Enter a closing time later than the opening time on the same day."));
            }
        }

        Rules.ThrowIfAny(errors);
        if (Status == LocationStatus.Active && week.Count == 0)
        {
            throw new RefusalException(new FieldError("days", LocationsErrors.ValidationFailed, "An active location needs opening hours for at least one day."));
        }

        foreach (var removed in _operatingHours.Where(h => !seen.Contains(h.DayOfWeek)).ToList())
        {
            _operatingHours.Remove(removed);
        }

        foreach (var (day, opensAt, closesAt) in week)
        {
            var existing = _operatingHours.FirstOrDefault(h => h.DayOfWeek == day);
            if (existing is null)
            {
                _operatingHours.Add(new OperatingHours(Id, day, opensAt, closesAt));
            }
            else
            {
                existing.Change(opensAt, closesAt);
            }
        }

        Raise(new OperatingHoursChanged(Id));
    }

    /// <summary>LOC-R14: a closure on <paramref name="closedOn"/> (today or later, location time zone), one per date.</summary>
    public LocationClosure AddClosure(DateOnly closedOn, string? reason, DateOnly today)
    {
        if (closedOn < today)
        {
            throw new RefusalException(new FieldError("closedOn", LocationsErrors.ValidationFailed, "Enter today or a later date for the closure."));
        }

        if (reason is { Length: > LocationClosure.ReasonMaxLength })
        {
            throw new RefusalException(new FieldError("reason", LocationsErrors.ValidationFailed, $"Enter a reason of at most {LocationClosure.ReasonMaxLength} characters."));
        }

        if (_closures.Any(c => c.ClosedOn == closedOn))
        {
            throw new ConflictException(LocationsErrors.Duplicate, "The location already has a closure on this date.", "closedOn");
        }

        var closure = new LocationClosure(Id, closedOn, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
        _closures.Add(closure);
        Raise(new LocationClosureAdded(Id, closedOn));
        return closure;
    }

    /// <summary>Only a future closure (after today) can be removed; a past or today's closure is history.</summary>
    public void RemoveClosure(Guid closureId, DateOnly today)
    {
        var closure = _closures.FirstOrDefault(c => c.Id == closureId)
            ?? throw new NotFoundException(LocationsErrors.ClosureNotFound, "The location has no closure with this id.");
        if (closure.ClosedOn <= today)
        {
            throw new ConflictException(LocationsErrors.InvalidStatusTransition, "Only a closure on a future date can be removed.", "closureId");
        }

        _closures.Remove(closure);
        Raise(new LocationClosureRemoved(Id, closure.ClosedOn));
    }

    private static void ValidateDetails(List<FieldError> errors, LocationDetails details)
    {
        Rules.RequireName(errors, "name", details.Name);
        if (details.AddressLine is { Length: > AddressMaxLength })
        {
            errors.Add(new FieldError("addressLine", LocationsErrors.ValidationFailed, $"Enter an address of at most {AddressMaxLength} characters."));
        }

        if (details.Pincode is not null && !Rules.IsPincode(details.Pincode))
        {
            errors.Add(new FieldError("pincode", LocationsErrors.ValidationFailed, "Enter a 6-digit pincode that does not start with 0."));
        }

        if (details.Phone is { Length: > PhoneMaxLength })
        {
            errors.Add(new FieldError("phone", LocationsErrors.ValidationFailed, $"Enter a phone number of at most {PhoneMaxLength} characters."));
        }

        if (details.Latitude is < -90 or > 90)
        {
            errors.Add(new FieldError("latitude", LocationsErrors.ValidationFailed, "Enter a latitude between -90 and 90."));
        }

        if (details.Longitude is < -180 or > 180)
        {
            errors.Add(new FieldError("longitude", LocationsErrors.ValidationFailed, "Enter a longitude between -180 and 180."));
        }

        if (details.FssaiLicenseNo is { Length: > FssaiMaxLength })
        {
            errors.Add(new FieldError("fssaiLicenseNo", LocationsErrors.ValidationFailed, $"Enter an FSSAI licence number of at most {FssaiMaxLength} characters."));
        }
    }

    private List<FieldError> ActivationErrors(bool cityRetired)
    {
        var errors = new List<FieldError>();
        if (cityRetired)
        {
            errors.Add(new FieldError("cityId", LocationsErrors.ValidationFailed, "A location of a retired city cannot be activated."));
        }

        if (string.IsNullOrWhiteSpace(AddressLine))
        {
            errors.Add(new FieldError("addressLine", LocationsErrors.ValidationFailed, "Enter the address before activating the location."));
        }

        if (string.IsNullOrWhiteSpace(Pincode))
        {
            errors.Add(new FieldError("pincode", LocationsErrors.ValidationFailed, "Enter the pincode before activating the location."));
        }

        if (Latitude is null || Longitude is null)
        {
            errors.Add(new FieldError("latitude", LocationsErrors.ValidationFailed, "Enter latitude and longitude before activating the location."));
        }

        if (string.IsNullOrWhiteSpace(FssaiLicenseNo))
        {
            errors.Add(new FieldError("fssaiLicenseNo", LocationsErrors.ValidationFailed, "Enter the FSSAI licence number before activating the location."));
        }

        if (!HasColdStorage)
        {
            errors.Add(new FieldError("hasColdStorage", LocationsErrors.ValidationFailed, "A location needs cold storage before it can be activated."));
        }

        if (_operatingHours.Count == 0)
        {
            errors.Add(new FieldError("operatingHours", LocationsErrors.ValidationFailed, "Set opening hours for at least one day before activating the location."));
        }

        return errors;
    }

    private void Apply(LocationDetails details)
    {
        Name = details.Name!.Trim();
        AddressLine = Normalize(details.AddressLine);
        Pincode = Normalize(details.Pincode);
        Phone = Normalize(details.Phone);
        Latitude = details.Latitude;
        Longitude = details.Longitude;
        HasColdStorage = details.HasColdStorage;
        FssaiLicenseNo = Normalize(details.FssaiLicenseNo);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Opening hours of one day of the week, in the location's time zone.</summary>
public sealed class OperatingHours
{
    private OperatingHours()
    {
    }

    internal OperatingHours(Guid fulfillmentLocationId, DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
    {
        FulfillmentLocationId = fulfillmentLocationId;
        DayOfWeek = dayOfWeek;
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }

    public Guid FulfillmentLocationId { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly OpensAt { get; private set; }

    public TimeOnly ClosesAt { get; private set; }

    internal void Change(TimeOnly opensAt, TimeOnly closesAt)
    {
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }
}

/// <summary>A date the location does not deliver (holiday, maintenance).</summary>
public sealed class LocationClosure : Entity
{
    public const int ReasonMaxLength = 200;

    private LocationClosure()
    {
    }

    internal LocationClosure(Guid fulfillmentLocationId, DateOnly closedOn, string? reason)
    {
        FulfillmentLocationId = fulfillmentLocationId;
        ClosedOn = closedOn;
        Reason = reason;
    }

    public Guid FulfillmentLocationId { get; private set; }

    public DateOnly ClosedOn { get; private set; }

    public string? Reason { get; private set; }
}
