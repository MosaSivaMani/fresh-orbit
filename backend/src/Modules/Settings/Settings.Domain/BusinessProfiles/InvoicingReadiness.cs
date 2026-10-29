namespace FreshOrbit.Settings.Domain.BusinessProfiles;

/// <summary>
/// What an invoice needs from the business profile (BE-1, D295, SET-R9): the seller's legal name and GSTIN, and the
/// business time zone (financial year of the INVOICE number). The store takes no order while one is missing, so a
/// delivery can always issue its invoice (rule 7: missing configuration is refused up front, not found at delivery).
/// </summary>
public static class InvoicingReadiness
{
    /// <summary>Reported when no business profile has been saved at all.</summary>
    public const string ProfileField = "businessProfile";

    public const string LegalNameField = "legalName";
    public const string GstinField = "gstin";
    public const string TimeZoneField = "timeZone";

    /// <summary>The camelCase profile fields invoicing still needs, in form order; empty when the store is ready.</summary>
    public static IReadOnlyList<string> MissingFields(BusinessProfile? profile)
        => profile is null ? [ProfileField] : MissingFields(profile.LegalName, profile.Gstin, profile.TimeZone);

    /// <inheritdoc cref="MissingFields(BusinessProfile?)"/>
    public static IReadOnlyList<string> MissingFields(string? legalName, string? gstin, string? timeZone)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(legalName))
        {
            missing.Add(LegalNameField);
        }

        if (string.IsNullOrWhiteSpace(gstin))
        {
            missing.Add(GstinField);
        }

        if (string.IsNullOrWhiteSpace(timeZone))
        {
            missing.Add(TimeZoneField);
        }

        return missing;
    }
}
