using FluentValidation;
using FreshOrbit.Settings.Application.Shared;
using FreshOrbit.Settings.Domain.BusinessProfiles;
using FreshOrbit.Settings.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Settings.Application.BusinessProfiles;

public sealed record SaveBusinessProfileRequest(
    string LegalName,
    string TradeName,
    string? Gstin,
    string? FssaiLicenseNo,
    string Address,
    string SupportPhone,
    string SupportEmail,
    string TimeZone);

public sealed record BusinessProfileResponse(
    string LegalName,
    string TradeName,
    string? Gstin,
    string? FssaiLicenseNo,
    string Address,
    string SupportPhone,
    string SupportEmail,
    string TimeZone,
    string Version);

public sealed class SaveBusinessProfileRequestValidator : AbstractValidator<SaveBusinessProfileRequest>
{
    public SaveBusinessProfileRequestValidator()
    {
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TradeName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.SupportPhone).NotEmpty().Matches(@"^\+[1-9][0-9]{7,14}$").WithErrorCode("PHONE_INVALID")
            .WithMessage("The supportPhone must be an E.164 number such as +919876543210.");
        RuleFor(x => x.SupportEmail).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.FssaiLicenseNo).Matches("^[0-9]{14}$").When(x => !string.IsNullOrWhiteSpace(x.FssaiLicenseNo))
            .WithErrorCode("FSSAI_INVALID").WithMessage("The fssaiLicenseNo must be 14 digits.");
        RuleFor(x => x.TimeZone).NotEmpty().Must(BeKnownTimeZone).WithErrorCode("TIME_ZONE_INVALID")
            .WithMessage("The timeZone must be an IANA time zone id such as Asia/Kolkata.");
    }

    private static bool BeKnownTimeZone(string? id) => !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}

public interface IBusinessProfileService
{
    Task<BusinessProfileResponse> GetAsync(CancellationToken cancellationToken);

    Task<BusinessProfileResponse> SaveAsync(SaveBusinessProfileRequest request, WriteCondition condition, CancellationToken cancellationToken);
}

public sealed class BusinessProfileService(ISettingsRepository repository, IUnitOfWork unitOfWork) : IBusinessProfileService
{
    public async Task<BusinessProfileResponse> GetAsync(CancellationToken cancellationToken)
    {
        var profile = await repository.GetBusinessProfileAsync(cancellationToken)
            ?? throw new NotFoundException("The business profile has not been entered yet.");
        return ToResponse(profile);
    }

    public async Task<BusinessProfileResponse> SaveAsync(SaveBusinessProfileRequest request, WriteCondition condition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(condition);
        var existing = await repository.GetBusinessProfileAsync(cancellationToken);
        var data = new BusinessProfileData(request.LegalName, request.TradeName, request.Gstin, request.FssaiLicenseNo, request.Address, request.SupportPhone, request.SupportEmail, request.TimeZone);
        BusinessProfile profile;
        if (condition.ShouldCreate(existing, unitOfWork, "business profile"))
        {
            profile = BusinessProfile.Create(data);
            repository.Add(profile);
        }
        else
        {
            profile = existing!;
            profile.Update(data);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(profile);
    }

    public static BusinessProfileResponse ToResponse(BusinessProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new BusinessProfileResponse(
            profile.LegalName,
            profile.TradeName,
            profile.Gstin,
            profile.FssaiLicenseNo,
            profile.Address,
            profile.SupportPhone,
            profile.SupportEmail,
            profile.TimeZone,
            profile.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
