using FluentValidation;
using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Delivery.Domain.Riders;
using FreshOrbit.Delivery.Domain.Shared;
using FreshOrbit.Delivery.Domain.Shipments;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Delivery.Application.Riders;

public sealed record CreateRiderRequest(Guid FulfillmentLocationId, string FullName, string Phone);

public sealed record UpdateRiderRequest(string FullName, string Phone, bool IsActive);

internal static class RiderRules
{
    public const string PhonePattern = @"^\+[1-9][0-9]{7,14}$";
}

public sealed class CreateRiderRequestValidator : AbstractValidator<CreateRiderRequest>
{
    public CreateRiderRequestValidator()
    {
        RuleFor(x => x.FulfillmentLocationId).NotEmpty().WithErrorCode(DeliveryErrors.ValidationFailed);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(Rider.MaxFullNameLength).WithErrorCode(DeliveryErrors.ValidationFailed)
            .WithMessage("Full name must be 1 to 120 characters.");
        RuleFor(x => x.Phone).NotEmpty().Matches(RiderRules.PhonePattern).WithErrorCode(DeliveryErrors.ValidationFailed)
            .WithMessage("Phone must be in E.164 format, e.g. +919876543210.");
    }
}

public sealed class UpdateRiderRequestValidator : AbstractValidator<UpdateRiderRequest>
{
    public UpdateRiderRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(Rider.MaxFullNameLength).WithErrorCode(DeliveryErrors.ValidationFailed)
            .WithMessage("Full name must be 1 to 120 characters.");
        RuleFor(x => x.Phone).NotEmpty().Matches(RiderRules.PhonePattern).WithErrorCode(DeliveryErrors.ValidationFailed)
            .WithMessage("Phone must be in E.164 format, e.g. +919876543210.");
    }
}

/// <summary>Riders (DEL-R4, §2 rider).</summary>
public sealed class RiderService(IRiderRepository riders, IShipmentRepository shipments, ILocationCalendar calendar, IUnitOfWork unitOfWork, ICurrentActor actor)
{
    public async Task<Rider> CreateAsync(CreateRiderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        actor.EnsureLocation(request.FulfillmentLocationId);
        await calendar.GetRequiredLocationAsync(request.FulfillmentLocationId, "fulfillmentLocationId", cancellationToken);
        await EnsurePhoneFreeAsync(request.Phone, null, cancellationToken);
        var rider = Rider.Create(request.FulfillmentLocationId, request.FullName, request.Phone);
        riders.Add(rider);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return rider;
    }

    public async Task<Rider> UpdateAsync(Guid riderId, UpdateRiderRequest request, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rider = await riders.GetAsync(riderId, cancellationToken) ?? throw new NotFoundException("No rider has this id.");
        actor.EnsureLocation(rider.FulfillmentLocationId);
        unitOfWork.SetExpectedVersion(rider, expectedVersion);
        if (request.IsActive)
        {
            await EnsurePhoneFreeAsync(request.Phone, rider.Id, cancellationToken);
        }
        else if (rider.IsActive && await shipments.CountOpenForRiderAsync(rider.Id, cancellationToken) > 0)
        {
            throw new ConflictException(DeliveryErrors.RiderHasActiveShipments, "The rider still has assigned, out-for-delivery or failed shipments; reassign them first.", "isActive");
        }

        rider.Update(request.FullName, request.Phone, request.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return rider;
    }

    private async Task EnsurePhoneFreeAsync(string phone, Guid? exceptRiderId, CancellationToken cancellationToken)
    {
        if (await riders.ActivePhoneExistsAsync(phone, exceptRiderId, cancellationToken))
        {
            throw new ConflictException(DeliveryErrors.Duplicate, "Another active rider has this phone number.", "phone");
        }
    }
}
