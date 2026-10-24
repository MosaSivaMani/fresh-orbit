using FluentValidation;
using FreshOrbit.Audit.Public;
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
    string TimeZone,
    string? SupportHours = null);

public sealed record BusinessProfileResponse(
    string LegalName,
    string TradeName,
    string? Gstin,
    string? FssaiLicenseNo,
    string Address,
    string SupportPhone,
    string SupportEmail,
    string TimeZone,
    string Version)
{
    public string? SupportHours { get; init; }
}

/// <summary>
/// <c>GET /api/v1/business-profile</c> (public, W7): who the seller is and how to reach support. GSTIN, FSSAI licence
/// number, legal name and address are printed on every invoice and the FSSAI number must be shown to e-commerce
/// customers, so they are public; nothing else of the profile (version, internal fields) is exposed.
/// </summary>
public sealed record PublicBusinessProfileResponse(
    string LegalName,
    string TradeName,
    string? Gstin,
    string? FssaiLicenseNo,
    string Address,
    string SupportPhone,
    string SupportEmail,
    string? SupportHours,
    string TimeZone);

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
        RuleFor(x => x.SupportHours).MaximumLength(200);
        RuleFor(x => x.TimeZone).NotEmpty().Must(BeKnownTimeZone).WithErrorCode("TIME_ZONE_INVALID")
            .WithMessage("The timeZone must be an IANA time zone id such as Asia/Kolkata.");
    }

    private static bool BeKnownTimeZone(string? id) => !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}

public interface IBusinessProfileService
{
    Task<BusinessProfileResponse> GetAsync(CancellationToken cancellationToken);

    /// <summary>The public part (W7); 404 BUSINESS_PROFILE_NOT_CONFIGURED until an admin saves the profile (rule 7).</summary>
    Task<PublicBusinessProfileResponse> GetPublicAsync(CancellationToken cancellationToken);

    Task<BusinessProfileResponse> SaveAsync(SaveBusinessProfileRequest request, WriteCondition condition, CancellationToken cancellationToken);
}

public sealed class BusinessProfileService(ISettingsRepository repository, IUnitOfWork unitOfWork, IAuditApi audit) : IBusinessProfileService
{
    public async Task<BusinessProfileResponse> GetAsync(CancellationToken cancellationToken)
    {
        var profile = await repository.GetBusinessProfileAsync(cancellationToken)
            ?? throw new NotFoundException("The business profile has not been entered yet.");
        return ToResponse(profile);
    }

    public async Task<PublicBusinessProfileResponse> GetPublicAsync(CancellationToken cancellationToken)
    {
        var profile = await repository.GetBusinessProfileAsync(cancellationToken)
            ?? throw new NotFoundException(SettingsErrors.BusinessProfileNotConfigured, "The business profile has not been entered yet.");
        return new PublicBusinessProfileResponse(
            profile.LegalName,
            profile.TradeName,
            profile.Gstin,
            profile.FssaiLicenseNo,
            profile.Address,
            profile.SupportPhone,
            profile.SupportEmail,
            profile.SupportHours,
            profile.TimeZone);
    }

    public async Task<BusinessProfileResponse> SaveAsync(SaveBusinessProfileRequest request, WriteCondition condition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(condition);
        var existing = await repository.GetBusinessProfileAsync(cancellationToken);
        var data = new BusinessProfileData(request.LegalName, request.TradeName, request.Gstin, request.FssaiLicenseNo, request.Address, request.SupportPhone, request.SupportEmail, request.TimeZone, request.SupportHours);
        var before = existing is null ? null : ToResponse(existing);
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

        audit.Record(new AuditRecord("BUSINESS_PROFILE_CHANGED", "BUSINESS_PROFILE", "default", before, ToResponse(profile)));
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
            profile.Version.ToString(System.Globalization.CultureInfo.InvariantCulture))
        {
            SupportHours = profile.SupportHours,
        };
    }
}
