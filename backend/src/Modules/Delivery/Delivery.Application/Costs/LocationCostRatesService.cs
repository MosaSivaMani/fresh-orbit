using System.Globalization;
using FluentValidation;
using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Delivery.Domain.Costs;
using FreshOrbit.Delivery.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Delivery.Application.Costs;

/// <summary>
/// Body of <c>PUT /admin/delivery/locations/{fulfillmentLocationId}/cost-rates</c> (D365). Both fields are sent;
/// null clears a value back to "not configured".
/// </summary>
/// <param name="DeliveryCostPerDropPaise">Rider pay per delivered order, 0–100 000 paise, or null when not known.</param>
/// <param name="PackagingCostPerOrderPaise">Packaging and ice per delivered order, 0–50 000 paise, or null when not known.</param>
public sealed record SaveLocationCostRatesRequest(long? DeliveryCostPerDropPaise, long? PackagingCostPerOrderPaise);

/// <summary>A location's cost rates; <c>Version</c> is null (and both costs null) until they are first saved.</summary>
/// <param name="Configured">Both costs are set, so every order delivered from now on carries a full cost.</param>
public sealed record LocationCostRatesResponse(
    Guid FulfillmentLocationId,
    long? DeliveryCostPerDropPaise,
    long? PackagingCostPerOrderPaise,
    bool Configured,
    string? Version);

public sealed class SaveLocationCostRatesRequestValidator : AbstractValidator<SaveLocationCostRatesRequest>
{
    public SaveLocationCostRatesRequestValidator()
    {
        RuleFor(x => x.DeliveryCostPerDropPaise).InclusiveBetween(0, LocationCostRates.MaxDeliveryCostPerDropPaise)
            .WithErrorCode(DeliveryErrors.ValidationFailed)
            .WithMessage("The deliveryCostPerDropPaise must be between 0 and 100000 paise (₹1,000), or empty when not known.");
        RuleFor(x => x.PackagingCostPerOrderPaise).InclusiveBetween(0, LocationCostRates.MaxPackagingCostPerOrderPaise)
            .WithErrorCode(DeliveryErrors.ValidationFailed)
            .WithMessage("The packagingCostPerOrderPaise must be between 0 and 50000 paise (₹500), or empty when not known.");
    }
}

/// <summary>
/// The HTTP precondition of a create-or-update: <c>If-Match</c> (update that version) or <c>If-None-Match: *</c>
/// (create; refused when the rates exist).
/// </summary>
public sealed record CostRatesWriteCondition(uint? ExpectedVersion, bool CreateOnly);

/// <summary>Per-location delivery and packaging cost rates (D365): read and save, one save per command.</summary>
public sealed class LocationCostRatesService(ILocationCostRatesRepository rates, ILocationCalendar calendar, IUnitOfWork unitOfWork, ICurrentActor actor)
{
    public const string VersionRequiredCode = "VERSION_REQUIRED";

    public async Task<LocationCostRatesResponse> GetAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken)
    {
        actor.EnsureLocation(fulfillmentLocationId);
        await RequireLocationAsync(fulfillmentLocationId, cancellationToken);
        var existing = await rates.GetByLocationAsync(fulfillmentLocationId, cancellationToken);
        return existing is null
            ? new LocationCostRatesResponse(fulfillmentLocationId, null, null, false, null)
            : ToResponse(existing);
    }

    public async Task<LocationCostRates> SaveAsync(Guid fulfillmentLocationId, SaveLocationCostRatesRequest request, CostRatesWriteCondition condition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(condition);
        actor.EnsureLocation(fulfillmentLocationId);
        await RequireLocationAsync(fulfillmentLocationId, cancellationToken);
        var existing = await rates.GetByLocationAsync(fulfillmentLocationId, cancellationToken);
        if (existing is null)
        {
            if (!condition.CreateOnly)
            {
                throw condition.ExpectedVersion is null
                    ? new DomainException(VersionRequiredCode, "Send If-None-Match: * to set this location's costs for the first time.", "If-None-Match")
                    : new NotFoundException("This location has no cost rates yet; create them with If-None-Match: *.");
            }

            var created = LocationCostRates.Create(fulfillmentLocationId, request.DeliveryCostPerDropPaise, request.PackagingCostPerOrderPaise);
            rates.Add(created);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return created;
        }

        if (condition.CreateOnly)
        {
            throw new ConflictException(DeliveryErrors.Duplicate, "This location's cost rates already exist; update them with If-Match.");
        }

        if (condition.ExpectedVersion is not { } version)
        {
            throw new DomainException(VersionRequiredCode, "Send the current version of the cost rates in the If-Match header.", "If-Match");
        }

        unitOfWork.SetExpectedVersion(existing, version);
        existing.Update(request.DeliveryCostPerDropPaise, request.PackagingCostPerOrderPaise);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public static LocationCostRatesResponse ToResponse(LocationCostRates rates)
    {
        ArgumentNullException.ThrowIfNull(rates);
        return new LocationCostRatesResponse(
            rates.FulfillmentLocationId,
            rates.DeliveryCostPerDropPaise,
            rates.PackagingCostPerOrderPaise,
            rates.DeliveryCostPerDropPaise is not null && rates.PackagingCostPerOrderPaise is not null,
            rates.Version.ToString(CultureInfo.InvariantCulture));
    }

    private async Task RequireLocationAsync(Guid fulfillmentLocationId, CancellationToken cancellationToken)
    {
        var found = await calendar.GetLocationsAsync([fulfillmentLocationId], cancellationToken);
        if (!found.Any(l => l.Id == fulfillmentLocationId))
        {
            throw new NotFoundException("No fulfillment location has this id.");
        }
    }
}
