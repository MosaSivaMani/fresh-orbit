using FluentValidation;
using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Delivery.Domain.Riders;
using FreshOrbit.Delivery.Domain.Shared;
using FreshOrbit.Delivery.Domain.Shipments;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Delivery.Application.Shipments;

public sealed record AssignRiderRequest(Guid RiderId);

public sealed record DispatchShipmentRequest(decimal? DispatchTemperatureC);

/// <param name="DeliveryCode">The 4-digit code the customer reads out.</param>
/// <param name="CashCollectedPaise">Cash or UPI collected at the door (A7); needs <paramref name="CashMode"/>.</param>
public sealed record DeliverShipmentRequest(string DeliveryCode, long? CashCollectedPaise, CashMode? CashMode, string? UpiReference);

public sealed record RecordAttemptRequest(AttemptOutcome Outcome, string? Notes);

public sealed class AssignRiderRequestValidator : AbstractValidator<AssignRiderRequest>
{
    public AssignRiderRequestValidator() => RuleFor(x => x.RiderId).NotEmpty().WithErrorCode(DeliveryErrors.ValidationFailed);
}

public sealed class DispatchShipmentRequestValidator : AbstractValidator<DispatchShipmentRequest>
{
    public DispatchShipmentRequestValidator()
        => RuleFor(x => x.DispatchTemperatureC).NotNull().InclusiveBetween(Shipment.MinTemperatureC, Shipment.MaxTemperatureC)
            .WithErrorCode(DeliveryErrors.ValidationFailed).WithMessage("Dispatch temperature must be between -40.0 and 40.0 °C.");
}

public sealed class DeliverShipmentRequestValidator : AbstractValidator<DeliverShipmentRequest>
{
    public DeliverShipmentRequestValidator()
    {
        RuleFor(x => x.DeliveryCode).NotEmpty().Matches("^[0-9]{4}$").WithErrorCode(DeliveryErrors.ValidationFailed)
            .WithMessage("Delivery code must be 4 digits.");
        RuleFor(x => x.CashCollectedPaise).GreaterThanOrEqualTo(0).WithErrorCode(DeliveryErrors.ValidationFailed)
            .WithMessage("Cash collected must not be negative.");
        RuleFor(x => x.CashMode).NotNull().When(x => x.CashCollectedPaise is not null).WithErrorCode(DeliveryErrors.ValidationFailed)
            .WithMessage("Cash mode (CASH or UPI) is required with cash collected.");
        RuleFor(x => x.UpiReference).NotEmpty().When(x => x.CashMode == CashMode.Upi).WithErrorCode(DeliveryErrors.ValidationFailed)
            .WithMessage("A UPI collection needs the UPI reference.");
        RuleFor(x => x.UpiReference).MaximumLength(Shipment.MaxUpiReferenceLength).WithErrorCode(DeliveryErrors.ValidationFailed)
            .WithMessage("The UPI reference must be at most 100 characters.");
    }
}

/// <summary>Shipment commands of the dispatcher (DEL-R5 … R18). Each loads, checks If-Match, decides and saves once.</summary>
public sealed class ShipmentService(
    IShipmentRepository shipments,
    IRiderRepository riders,
    IDeliverySlotRepository slots,
    ILocationCalendar calendar,
    IDeliveryPolicyProvider policy,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    IClock clock)
{
    public async Task<Shipment> AssignAsync(Guid shipmentId, AssignRiderRequest request, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = await LoadAsync(shipmentId, expectedVersion, cancellationToken);
        var rider = await riders.GetAsync(request.RiderId, cancellationToken)
            ?? throw new DomainException(DeliveryErrors.RiderNotAvailable, "No rider has this id; choose another rider.", "riderId");
        shipment.AssignRider(rider);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment;
    }

    public async Task<Shipment> DispatchAsync(Guid shipmentId, DispatchShipmentRequest request, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = await LoadAsync(shipmentId, expectedVersion, cancellationToken);
        var rider = shipment.RiderId is { } riderId ? await riders.GetAsync(riderId, cancellationToken) : null;
        var temperature = request.DispatchTemperatureC ?? throw DeliveryErrors.Invalid("dispatchTemperatureC", "Dispatch temperature is required.");
        shipment.Dispatch(rider, temperature, Shipment.NewDeliveryCode(), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment;
    }

    /// <summary>A wrong code is saved (failure counter) and then refused with DELIVERY_CODE_INVALID.</summary>
    public async Task<Shipment> DeliverAsync(Guid shipmentId, DeliverShipmentRequest request, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = await LoadAsync(shipmentId, expectedVersion, cancellationToken);
        CashReceipt? cash = request.CashCollectedPaise is { } amount
            ? new CashReceipt(amount, request.CashMode ?? throw DeliveryErrors.Invalid("cashMode", "Cash mode (CASH or UPI) is required with cash collected."), request.UpiReference?.Trim())
            : null;
        var outcome = shipment.Deliver(request.DeliveryCode, cash, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (outcome == DeliverOutcome.CodeInvalid)
        {
            throw new DomainException(DeliveryErrors.DeliveryCodeInvalid, "The delivery code is wrong; ask the customer for the 4-digit code again.", "deliveryCode");
        }

        return shipment;
    }

    public async Task<Shipment> RecordFailedAttemptAsync(Guid shipmentId, RecordAttemptRequest request, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = await LoadAsync(shipmentId, expectedVersion, cancellationToken);
        shipment.RecordFailedAttempt(request.Outcome, request.Notes, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment;
    }

    public async Task<Shipment> ReattemptAsync(Guid shipmentId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var shipment = await LoadAsync(shipmentId, expectedVersion, cancellationToken);
        var slot = await slots.GetAsync(shipment.DeliverySlotId, cancellationToken)
            ?? throw new InvalidOperationException($"Slot {shipment.DeliverySlotId} of shipment {shipment.Id} is missing.");
        var location = await calendar.GetRequiredLocationAsync(shipment.FulfillmentLocationId, "fulfillmentLocationId", cancellationToken);
        var maxAttempts = await policy.GetMaxDeliveryAttemptsAsync(shipment.FulfillmentLocationId, cancellationToken);
        shipment.Reattempt(maxAttempts, SlotSchedule.LocalDate(clock.UtcNow, location.TimeZone()), slot.SlotDate);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment;
    }

    public async Task<Shipment> ReturnAsync(Guid shipmentId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var shipment = await LoadAsync(shipmentId, expectedVersion, cancellationToken);
        shipment.Return(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment;
    }

    private async Task<Shipment> LoadAsync(Guid shipmentId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var shipment = await shipments.GetAsync(shipmentId, cancellationToken) ?? throw new NotFoundException("No shipment has this id.");
        actor.EnsureLocation(shipment.FulfillmentLocationId);
        unitOfWork.SetExpectedVersion(shipment, expectedVersion);
        return shipment;
    }
}
