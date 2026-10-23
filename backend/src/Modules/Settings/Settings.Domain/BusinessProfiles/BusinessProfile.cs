using FreshOrbit.Settings.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Settings.Domain.BusinessProfiles;

/// <summary>The seller's legal identity and support contacts (single row).</summary>
public sealed class BusinessProfile : AggregateRoot
{
    private BusinessProfile()
    {
        LegalName = TradeName = Address = SupportPhone = SupportEmail = TimeZone = string.Empty;
    }

    public string LegalName { get; private set; }

    public string TradeName { get; private set; }

    public string? Gstin { get; private set; }

    public string? FssaiLicenseNo { get; private set; }

    public string Address { get; private set; }

    public string SupportPhone { get; private set; }

    public string SupportEmail { get; private set; }

    /// <summary>IANA time zone id, e.g. Asia/Kolkata.</summary>
    public string TimeZone { get; private set; }

    public static BusinessProfile Create(BusinessProfileData data)
    {
        var profile = new BusinessProfile();
        profile.Update(data);
        return profile;
    }

    public void Update(BusinessProfileData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var gstin = string.IsNullOrWhiteSpace(data.Gstin) ? null : data.Gstin.Trim().ToUpperInvariant();
        if (gstin is not null && !Gstins.IsValid(gstin))
        {
            throw new DomainException(SettingsErrors.GstinInvalid, "The gstin must be a valid 15-character GSTIN.", "gstin");
        }

        LegalName = data.LegalName.Trim();
        TradeName = data.TradeName.Trim();
        Gstin = gstin;
        FssaiLicenseNo = string.IsNullOrWhiteSpace(data.FssaiLicenseNo) ? null : data.FssaiLicenseNo.Trim();
        Address = data.Address.Trim();
        SupportPhone = data.SupportPhone.Trim();
        SupportEmail = data.SupportEmail.Trim();
        TimeZone = data.TimeZone.Trim();
    }
}

public sealed record BusinessProfileData(
    string LegalName,
    string TradeName,
    string? Gstin,
    string? FssaiLicenseNo,
    string Address,
    string SupportPhone,
    string SupportEmail,
    string TimeZone);
