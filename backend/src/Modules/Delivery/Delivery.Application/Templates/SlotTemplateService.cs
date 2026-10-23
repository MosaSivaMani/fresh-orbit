using System.Globalization;
using FluentValidation;
using FreshOrbit.Delivery.Application.Common;
using FreshOrbit.Delivery.Domain.Shared;
using FreshOrbit.Delivery.Domain.Templates;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Delivery.Application.Templates;

/// <param name="StartTime">HH:mm local time.</param>
public sealed record CreateSlotTemplateRequest(Guid FulfillmentLocationId, int DayOfWeek, string StartTime, string EndTime, int Capacity, int CutoffMinutes);

public sealed record UpdateSlotTemplateRequest(int DayOfWeek, string StartTime, string EndTime, int Capacity, int CutoffMinutes, bool IsActive);

public sealed class CreateSlotTemplateRequestValidator : AbstractValidator<CreateSlotTemplateRequest>
{
    public CreateSlotTemplateRequestValidator()
    {
        RuleFor(x => x.FulfillmentLocationId).NotEmpty().WithErrorCode(DeliveryErrors.ValidationFailed);
        RuleFor(x => x.StartTime).Must(TimeFormat.IsValid).WithErrorCode(DeliveryErrors.ValidationFailed).WithMessage("Start time must be HH:mm.");
        RuleFor(x => x.EndTime).Must(TimeFormat.IsValid).WithErrorCode(DeliveryErrors.ValidationFailed).WithMessage("End time must be HH:mm.");
    }
}

public sealed class UpdateSlotTemplateRequestValidator : AbstractValidator<UpdateSlotTemplateRequest>
{
    public UpdateSlotTemplateRequestValidator()
    {
        RuleFor(x => x.StartTime).Must(TimeFormat.IsValid).WithErrorCode(DeliveryErrors.ValidationFailed).WithMessage("Start time must be HH:mm.");
        RuleFor(x => x.EndTime).Must(TimeFormat.IsValid).WithErrorCode(DeliveryErrors.ValidationFailed).WithMessage("End time must be HH:mm.");
    }
}

public static class TimeFormat
{
    public static bool IsValid(string? value) => TryParse(value, out _);

    public static bool TryParse(string? value, out TimeOnly time)
        => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static TimeOnly Parse(string value, string field)
        => TryParse(value, out var time) ? time : throw DeliveryErrors.Invalid(field, "Times must be HH:mm.");

    public static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>Create / update slot templates (DEL-R1, R2, R8).</summary>
public sealed class SlotTemplateService(ISlotTemplateRepository templates, ILocationCalendar calendar, IUnitOfWork unitOfWork, ICurrentActor actor)
{
    public async Task<SlotTemplate> CreateAsync(CreateSlotTemplateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        actor.EnsureLocation(request.FulfillmentLocationId);
        await calendar.GetRequiredLocationAsync(request.FulfillmentLocationId, "fulfillmentLocationId", cancellationToken);
        var start = TimeFormat.Parse(request.StartTime, "startTime");
        var end = TimeFormat.Parse(request.EndTime, "endTime");
        var siblings = await templates.GetActiveByLocationAsync(request.FulfillmentLocationId, cancellationToken);
        var template = SlotTemplate.Create(request.FulfillmentLocationId, request.DayOfWeek, start, end, request.Capacity, request.CutoffMinutes);
        EnsureNoOverlap(siblings, template.Id, request.DayOfWeek, start, end);
        templates.Add(template);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task<SlotTemplate> UpdateAsync(Guid id, UpdateSlotTemplateRequest request, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var template = await templates.GetAsync(id, cancellationToken) ?? throw new NotFoundException("No slot template has this id.");
        actor.EnsureLocation(template.FulfillmentLocationId);
        unitOfWork.SetExpectedVersion(template, expectedVersion);
        var start = TimeFormat.Parse(request.StartTime, "startTime");
        var end = TimeFormat.Parse(request.EndTime, "endTime");
        SlotTemplate.Validate(request.DayOfWeek, start, end, request.Capacity, request.CutoffMinutes);
        if (request.IsActive)
        {
            var siblings = await templates.GetActiveByLocationAsync(template.FulfillmentLocationId, cancellationToken);
            EnsureNoOverlap(siblings, template.Id, request.DayOfWeek, start, end);
        }

        template.Update(request.DayOfWeek, start, end, request.Capacity, request.CutoffMinutes, request.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template;
    }

    /// <summary>DEL-R2 (the DB EXCLUDE constraint is the backstop, DEL-R22).</summary>
    public static void EnsureNoOverlap(IEnumerable<SlotTemplate> activeSiblings, Guid selfId, int dayOfWeek, TimeOnly start, TimeOnly end)
    {
        ArgumentNullException.ThrowIfNull(activeSiblings);
        if (activeSiblings.Any(t => t.Id != selfId && t.Overlaps(dayOfWeek, start, end)))
        {
            throw new ConflictException(DeliveryErrors.SlotTemplateOverlap, "The template overlaps another active template on the same day; change its times.", "startTime");
        }
    }
}
