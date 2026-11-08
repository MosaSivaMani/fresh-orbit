using AwesomeAssertions;
using FreshOrbit.Settings.Domain.BusinessProfiles;

namespace FreshOrbit.Settings.Tests.Unit;

/// <summary>BE-1 / D295 (SET-R9): the store takes orders only once the profile holds what an invoice needs.</summary>
public sealed class InvoicingReadinessTests
{
    private static string ValidGstin()
    {
        const string first14 = "33AAACF1234A1Z";
        return first14 + Gstins.CheckCharacter(first14);
    }

    private static BusinessProfile Profile(string? gstin, string legalName = "Fresh Orbit Foods Pvt Ltd", string timeZone = "Asia/Kolkata")
        => BusinessProfile.Create(new BusinessProfileData(legalName, "Fresh Orbit", gstin, null, "Chennai", "+914400000000", "care@example.com", timeZone));

    [Fact]
    public void Complete_profile_is_ready()
    {
        InvoicingReadiness.MissingFields(Profile(ValidGstin())).Should().BeEmpty();
    }

    [Fact]
    public void No_profile_is_reported_as_the_whole_profile_missing()
    {
        InvoicingReadiness.MissingFields((BusinessProfile?)null).Should().Equal(InvoicingReadiness.ProfileField);
    }

    [Fact]
    public void Profile_without_a_gstin_is_not_ready()
    {
        InvoicingReadiness.MissingFields(Profile(gstin: null)).Should().Equal(InvoicingReadiness.GstinField);
    }

    [Theory]
    [InlineData(null, null, null, new[] { "legalName", "gstin", "timeZone" })]
    [InlineData(" ", "33AAACF1234A1ZX", "Asia/Kolkata", new[] { "legalName" })]
    [InlineData("Fresh Orbit Foods", "", "Asia/Kolkata", new[] { "gstin" })]
    [InlineData("Fresh Orbit Foods", "33AAACF1234A1ZX", " ", new[] { "timeZone" })]
    public void Each_missing_field_is_named_in_form_order(string? legalName, string? gstin, string? timeZone, string[] expected)
    {
        InvoicingReadiness.MissingFields(legalName, gstin, timeZone).Should().Equal(expected);
    }
}
