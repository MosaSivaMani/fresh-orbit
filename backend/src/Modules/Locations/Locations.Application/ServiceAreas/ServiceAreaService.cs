using FreshOrbit.Locations.Application.Common;
using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.FulfillmentLocations;
using FreshOrbit.Locations.Domain.ServiceAreas;
using FreshOrbit.Locations.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Locations.Application.ServiceAreas;

/// <summary>Service area commands: fees (LOC-R4/R5) and pincodes (LOC-R2/R3).</summary>
public sealed class ServiceAreaService(
    IServiceAreaRepository areas,
    IFulfillmentLocationRepository locations,
    ICityRepository cities,
    IPincodeResolutionCache cache,
    IUnitOfWork unitOfWork,
    ICurrentActor actor)
{
    public async Task<ServiceAreaResponse> CreateAsync(CreateServiceAreaRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        LocationAccess.Ensure(actor, request.FulfillmentLocationId);
        _ = await cities.GetAsync(request.CityId, cancellationToken)
            ?? throw new RefusalException(new FieldError("cityId", LocationsErrors.ValidationFailed, "Choose an existing city."));
        var location = await locations.GetAsync(request.FulfillmentLocationId, cancellationToken)
            ?? throw new RefusalException(new FieldError("fulfillmentLocationId", LocationsErrors.ValidationFailed, "Choose an existing fulfillment location."));
        var area = ServiceArea.Create(
            request.CityId,
            location.Id,
            location.CityId,
            request.Name,
            new ServiceAreaFees(request.DeliveryFeePaise, request.FreeDeliveryAbovePaise, request.MinOrderPaise));
        areas.Add(area);
        await SaveAsync(cancellationToken);
        return ToResponse(area);
    }

    public async Task<ServiceAreaResponse> UpdateAsync(Guid serviceAreaId, uint expectedVersion, UpdateServiceAreaRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var area = await LoadAsync(serviceAreaId, expectedVersion, cancellationToken);
        area.Update(request.Name, new ServiceAreaFees(request.DeliveryFeePaise, request.FreeDeliveryAbovePaise, request.MinOrderPaise), request.IsActive);
        await SaveAsync(cancellationToken);
        return ToResponse(area);
    }

    /// <summary>All-or-nothing; the active rows of every given pincode are read in one query (LOC-R3).</summary>
    public async Task<AddPincodesResponse> AddPincodesAsync(Guid serviceAreaId, uint expectedVersion, AddPincodesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var area = await LoadAsync(serviceAreaId, expectedVersion, cancellationToken);
        var pincodes = request.Pincodes ?? [];
        var activeElsewhere = await areas.FindActiveElsewhereAsync([.. pincodes.OfType<string>().Distinct(StringComparer.Ordinal)], area.Id, cancellationToken);
        var added = area.AddPincodes(pincodes, activeElsewhere);
        areas.MarkChanged(area);
        await SaveAsync(cancellationToken);
        return new AddPincodesResponse(area.Id, added, area.Pincodes.Count(p => p.IsActive), Formats.Version(area.Version));
    }

    public async Task<ServiceAreaResponse> RemovePincodeAsync(Guid serviceAreaId, uint expectedVersion, string pincode, CancellationToken cancellationToken)
    {
        var area = await LoadAsync(serviceAreaId, expectedVersion, cancellationToken);
        area.RemovePincode(pincode);
        areas.MarkChanged(area);
        await SaveAsync(cancellationToken);
        return ToResponse(area);
    }

    public static ServiceAreaResponse ToResponse(ServiceArea area)
    {
        ArgumentNullException.ThrowIfNull(area);
        return new ServiceAreaResponse(
            area.Id,
            area.CityId,
            area.FulfillmentLocationId,
            area.Name,
            area.DeliveryFeePaise,
            area.FreeDeliveryAbovePaise,
            area.MinOrderPaise,
            area.IsActive,
            area.Pincodes.Count(p => p.IsActive),
            Formats.Version(area.Version));
    }

    private async Task<ServiceArea> LoadAsync(Guid serviceAreaId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var area = await areas.GetAsync(serviceAreaId, cancellationToken)
            ?? throw new NotFoundException(LocationsErrors.ServiceAreaNotFound, "No service area has this id.");
        LocationAccess.Ensure(actor, area.FulfillmentLocationId);
        unitOfWork.SetExpectedVersion(area, expectedVersion);
        return area;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
    }
}
