using FreshOrbit.Locations.Public.Events;
using FreshOrbit.Locations.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Locations.Domain.ServiceAreas;

/// <summary>Delivery terms of a service area (LOC-R5).</summary>
public sealed record ServiceAreaFees(long DeliveryFeePaise, long? FreeDeliveryAbovePaise, long MinOrderPaise);

/// <summary>A set of pincodes one fulfillment location delivers to, with its fee configuration.</summary>
public sealed class ServiceArea : AggregateRoot
{
    private readonly List<Pincode> _pincodes = [];

    private ServiceArea()
    {
        Name = string.Empty;
    }

    private ServiceArea(Guid cityId, Guid fulfillmentLocationId, string name, ServiceAreaFees fees)
    {
        CityId = cityId;
        FulfillmentLocationId = fulfillmentLocationId;
        Name = name;
        ApplyFees(fees);
        IsActive = true;
    }

    public Guid CityId { get; private set; }

    public Guid FulfillmentLocationId { get; private set; }

    public string Name { get; private set; }

    public long DeliveryFeePaise { get; private set; }

    public long? FreeDeliveryAbovePaise { get; private set; }

    public long MinOrderPaise { get; private set; }

    /// <summary>Deactivating keeps the pincodes, but they stop resolving.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Every pincode row, active and removed (history).</summary>
    public IReadOnlyList<Pincode> Pincodes => _pincodes;

    /// <summary>LOC-R4: the location must belong to <paramref name="cityId"/>; LOC-R5 fees.</summary>
    public static ServiceArea Create(Guid cityId, Guid fulfillmentLocationId, Guid locationCityId, string? name, ServiceAreaFees fees)
    {
        ArgumentNullException.ThrowIfNull(fees);
        var errors = new List<FieldError>();
        if (locationCityId != cityId)
        {
            errors.Add(new FieldError("fulfillmentLocationId", LocationsErrors.ValidationFailed, "Choose a fulfillment location of the same city as the service area."));
        }

        Rules.RequireName(errors, "name", name);
        ValidateFees(errors, fees);
        Rules.ThrowIfAny(errors);
        var area = new ServiceArea(cityId, fulfillmentLocationId, name!.Trim(), fees);
        area.Raise(area.Changed());
        return area;
    }

    public void Update(string? name, ServiceAreaFees fees, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(fees);
        var errors = new List<FieldError>();
        Rules.RequireName(errors, "name", name);
        ValidateFees(errors, fees);
        Rules.ThrowIfAny(errors);
        Name = name!.Trim();
        ApplyFees(fees);
        IsActive = isActive;
        Raise(Changed());
    }

    /// <summary>
    /// LOC-R2/R3, all-or-nothing. <paramref name="activeElsewhere"/> holds the given pincodes that are active in another
    /// service area. Pincodes already active here are left as they are. Returns the pincodes added.
    /// </summary>
    public IReadOnlyList<string> AddPincodes(IReadOnlyList<string?> pincodes, IReadOnlySet<string> activeElsewhere)
    {
        ArgumentNullException.ThrowIfNull(pincodes);
        ArgumentNullException.ThrowIfNull(activeElsewhere);
        var errors = new List<FieldError>();
        if (pincodes.Count == 0)
        {
            errors.Add(new FieldError("pincodes", LocationsErrors.ValidationFailed, "Enter at least one pincode."));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < pincodes.Count; i++)
        {
            var pincode = pincodes[i];
            if (!Rules.IsPincode(pincode))
            {
                errors.Add(new FieldError($"pincodes[{i}]", LocationsErrors.ValidationFailed, "Enter a 6-digit pincode that does not start with 0."));
            }
            else if (!seen.Add(pincode!))
            {
                errors.Add(new FieldError($"pincodes[{i}]", LocationsErrors.ValidationFailed, "Enter each pincode once."));
            }
        }

        Rules.ThrowIfAny(errors);
        for (var i = 0; i < pincodes.Count; i++)
        {
            if (activeElsewhere.Contains(pincodes[i]!))
            {
                throw new ConflictException(LocationsErrors.PincodeAlreadyAssigned, $"Pincode {pincodes[i]} is active in another service area; remove it there first.", $"pincodes[{i}]");
            }
        }

        var added = new List<string>();
        foreach (var pincode in pincodes.Select(p => p!))
        {
            if (_pincodes.Any(p => p.IsActive && p.Code == pincode))
            {
                continue;
            }

            _pincodes.Add(new Pincode(Id, pincode));
            added.Add(pincode);
        }

        if (added.Count > 0)
        {
            Raise(new PincodesChanged(Id, [.. added], []));
        }

        return added;
    }

    /// <summary>Deactivates the pincode (history kept; re-adding creates a new row).</summary>
    public void RemovePincode(string pincode)
    {
        var row = _pincodes.FirstOrDefault(p => p.IsActive && p.Code == pincode)
            ?? throw new NotFoundException(LocationsErrors.PincodeNotFound, "The service area has no active pincode with this value.");
        row.Deactivate();
        Raise(new PincodesChanged(Id, [], [pincode]));
    }

    private static void ValidateFees(List<FieldError> errors, ServiceAreaFees fees)
    {
        if (fees.DeliveryFeePaise < 0)
        {
            errors.Add(new FieldError("deliveryFeePaise", LocationsErrors.ValidationFailed, "Enter a delivery fee of 0 or more paise."));
        }

        if (fees.MinOrderPaise < 0)
        {
            errors.Add(new FieldError("minOrderPaise", LocationsErrors.ValidationFailed, "Enter a minimum order of 0 or more paise."));
        }

        if (fees.FreeDeliveryAbovePaise is { } free && free < fees.MinOrderPaise)
        {
            errors.Add(new FieldError("freeDeliveryAbovePaise", LocationsErrors.ValidationFailed, "Enter a free-delivery amount not lower than the minimum order, or leave it empty."));
        }
    }

    private void ApplyFees(ServiceAreaFees fees)
    {
        DeliveryFeePaise = fees.DeliveryFeePaise;
        FreeDeliveryAbovePaise = fees.FreeDeliveryAbovePaise;
        MinOrderPaise = fees.MinOrderPaise;
    }

    private ServiceAreaChanged Changed() => new(Id, DeliveryFeePaise, FreeDeliveryAbovePaise, MinOrderPaise, IsActive);
}

/// <summary>A pincode served by a service area. Active in at most one area (LOC-R3).</summary>
public sealed class Pincode : Entity
{
    public const int Length = 6;

    private Pincode()
    {
        Code = string.Empty;
    }

    internal Pincode(Guid serviceAreaId, string code)
    {
        ServiceAreaId = serviceAreaId;
        Code = code;
        IsActive = true;
    }

    public Guid ServiceAreaId { get; private set; }

    /// <summary>The six-digit pincode (column <c>pincode</c>).</summary>
    public string Code { get; private set; }

    public bool IsActive { get; private set; }

    internal void Deactivate() => IsActive = false;
}
