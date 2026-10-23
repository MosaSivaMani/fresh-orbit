using AwesomeAssertions;
using FreshOrbit.Settings.Domain.BusinessProfiles;
using FreshOrbit.Settings.Domain.Numbering;
using FreshOrbit.Settings.Domain.OrderPolicies;
using FreshOrbit.Settings.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Settings.Tests.Unit;

public sealed class GstinTests
{
    [Fact]
    public void Check_character_makes_a_gstin_valid()
    {
        var first14 = "33AAACF1234A1Z";
        var gstin = first14 + Gstins.CheckCharacter(first14);

        Gstins.IsValid(gstin).Should().BeTrue();
        Gstins.IsValid(first14 + (gstin[^1] == 'A' ? 'B' : 'A')).Should().BeFalse();
        Gstins.IsValid("33AAACF1234A1Z").Should().BeFalse();
    }
}

public sealed class FinancialYearTests
{
    [Theory]
    [InlineData(2026, 3, 31, "2025-26")]
    [InlineData(2026, 4, 1, "2026-27")]
    [InlineData(2099, 12, 31, "2099-00")]
    public void Label_follows_the_april_to_march_year(int year, int month, int day, string expected)
        => FinancialYear.LabelFor(new DateOnly(year, month, day)).Should().Be(expected);

    [Fact]
    public void Instant_is_seen_in_the_business_time_zone()
    {
        var india = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        // 31 March 20:00 UTC is 1 April 01:30 in India.
        FinancialYear.LabelFor(new DateTimeOffset(2027, 3, 31, 20, 0, 0, TimeSpan.Zero), india).Should().Be("2027-28");
    }

    [Theory]
    [InlineData("2026-27", true)]
    [InlineData("2026-28", false)]
    [InlineData("26-27", false)]
    public void Labels_are_validated(string label, bool valid) => FinancialYear.IsValidLabel(label).Should().Be(valid);
}

public sealed class NumberSeriesTests
{
    [Fact]
    public void Next_formats_and_advances()
    {
        var series = NumberSeries.Create("ORDER", "2026-27", "FO-26", 4, 7);

        series.Next(DateTimeOffset.UtcNow).Should().Be("FO-260007");
        series.Next(DateTimeOffset.UtcNow).Should().Be("FO-260008");
        series.NextValue.Should().Be(9);
    }

    [Fact]
    public void Format_cannot_change_once_a_number_was_issued()
    {
        var series = NumberSeries.Create("INVOICE", "2026-27", "INV/", 6, 1);
        series.Change("INV-", 6, 10);
        series.Next(DateTimeOffset.UtcNow);

        series.Invoking(s => s.Change("X", 6, 100)).Should().Throw<ConflictException>().Which.Code.Should().Be(SettingsErrors.NumberSeriesInUse);
    }

    [Fact]
    public void Unknown_code_and_bad_period_are_refused()
        => FluentActions.Invoking(() => NumberSeries.Create("RECEIPT", "2026", "R", 4, 1)).Should().Throw<RefusalException>()
            .Which.Errors.Select(e => e.Field).Should().Contain(["code", "period"]);
}

public sealed class OrderPolicyTests
{
    private static OrderPolicyData Valid() => new(15, 1000, "CONFIRMED", 300_000, ["COD", "ONLINE"], 10, 48, 2);

    [Fact]
    public void Valid_policy_has_no_errors() => OrderPolicy.Validate(Valid()).Should().BeEmpty();

    [Fact]
    public void Out_of_range_values_are_listed()
    {
        var data = Valid() with { ReservationMinutes = 1, CancelAllowedUntilStatus = "DELIVERED", EnabledPaymentMethods = ["CHEQUE"], MaxDeliveryAttempts = 9 };

        OrderPolicy.Validate(data).Select(e => e.Field).Should().BeEquivalentTo(["reservationMinutes", "cancelAllowedUntilStatus", "enabledPaymentMethods", "maxDeliveryAttempts"]);
    }
}
