using AwesomeAssertions;
using FreshOrbit.Pricing.Domain.Periods;
using FreshOrbit.Pricing.Domain.Periods.Events;
using FreshOrbit.Pricing.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Pricing.Tests.Unit;

/// <summary>pricing.md §6 U5–U9 and PRC-R7 basis rules.</summary>
public sealed class PeriodScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 6, 0, 0, TimeSpan.Zero);
    private static readonly Guid Location = Guid.CreateVersion7();
    private static readonly Guid Variant = Guid.CreateVersion7();

    private static PlannedChange<VariantPrice> Set(IReadOnlyList<VariantPrice> periods, long unitPrice, DateTimeOffset start, bool replaceFuture = false, long? compareAt = null)
        => PeriodFactory.PlanPrice(periods, Location, Variant, unitPrice, compareAt, start, Now, replaceFuture);

    private static VariantPrice Current(long unitPrice = 129900)
        => Set([], unitPrice, Now.AddDays(-3)).Added!;

    [Fact]
    public void Setting_a_price_inside_the_current_period_closes_it_and_opens_an_open_ended_one()
    {
        var current = Current();
        var start = Now.AddHours(6);

        var plan = Set([current], 124900, start);

        current.ValidTo.Should().Be(start);
        plan.Closed.Should().BeSameAs(current);
        plan.Added!.ValidFrom.Should().Be(start);
        plan.Added.ValidTo.Should().BeNull();
        plan.Added.DomainEvents.OfType<VariantPriceScheduled>().Single().Previous.Should().Be(new PriceValues(129900, null));
    }

    [Fact]
    public void Start_times_follow_prc_r3()
    {
        var errors = new List<FieldError>();
        PeriodSchedule.ResolveStart(Now.AddMinutes(-5), Now, "validFrom", errors);
        errors.Should().ContainSingle(e => e.Code == PricingErrors.PriceInPast);

        errors.Clear();
        PeriodSchedule.ResolveStart(Now.AddSeconds(-30), Now, "validFrom", errors).Should().Be(Now);
        errors.Should().BeEmpty();

        PeriodSchedule.ResolveStart(Now.AddDays(91), Now, "validFrom", errors);
        errors.Should().ContainSingle(e => e.Code == PricingErrors.ValidationFailed);

        PeriodSchedule.ResolveStart(null, Now, "validFrom", []).Should().Be(Now);
    }

    [Fact]
    public void A_start_is_cut_to_the_microseconds_the_database_stores()
    {
        var stored = Now.AddHours(2).AddTicks(1234560);
        var sent = stored.AddTicks(7);

        var start = PeriodSchedule.ResolveStart(sent, Now, "validFrom", []);

        start.Should().Be(stored);
        var scheduled = Set([Current()], 124900, start).Added!;
        Set([scheduled], 124900, PeriodSchedule.ResolveStart(sent, Now, "validFrom", [])).Unchanged.Should().BeTrue("a repeated request with the same start is unchanged");
    }

    [Fact]
    public void A_future_period_after_the_start_needs_replace_future()
    {
        var current = Current();
        var future = Set([current], 119900, Now.AddDays(2)).Added!;
        var start = Now.AddDays(1);

        var refused = () => Set([current, future], 124900, start);
        refused.Should().Throw<ConflictException>().Which.Code.Should().Be(PricingErrors.PricePeriodOverlap);

        var replaced = Set([current, future], 124900, start, replaceFuture: true);
        replaced.Removed.Should().ContainSingle().Which.Should().BeSameAs(future);
        replaced.Added!.ValidFrom.Should().Be(start);
    }

    [Fact]
    public void The_same_price_now_is_unchanged()
    {
        var current = Current();

        var plan = Set([current], 129900, Now);

        plan.Unchanged.Should().BeTrue();
        current.ValidTo.Should().BeNull();
    }

    [Theory]
    [InlineData(0, null, "unitPricePaise")]
    [InlineData(5_000_001, null, "unitPricePaise")]
    [InlineData(129900, 129900L, "compareAtPaise")]
    public void Price_values_follow_prc_r5(long unitPrice, long? compareAt, string field)
    {
        var errors = new List<FieldError>();
        VariantPrice.Validate(unitPrice, compareAt, string.Empty, errors);
        errors.Should().ContainSingle(e => e.Field == field);
    }

    [Theory]
    [InlineData(ChargeBasis.PerKg, true, true)]
    [InlineData(ChargeBasis.PerKg, false, false)]
    [InlineData(ChargeBasis.PerItem, false, true)]
    [InlineData(ChargeBasis.PerItem, true, false)]
    [InlineData(ChargeBasis.Flat, true, true)]
    [InlineData(ChargeBasis.Flat, false, true)]
    public void Charge_basis_must_suit_the_sell_unit(ChargeBasis basis, bool soldByWeight, bool valid)
    {
        var errors = new List<FieldError>();
        PreparationCharge.Validate(4000, basis, soldByWeight, string.Empty, errors);
        errors.Should().HaveCount(valid ? 0 : 1);
    }

    [Fact]
    public void Ending_a_price_closes_the_current_period_now()
    {
        var current = Current();

        PeriodFactory.EndPrice([current], Now).ValidTo.Should().Be(Now);
        current.DomainEvents.OfType<VariantPriceEnded>().Should().ContainSingle();

        var nothing = () => PeriodFactory.EndPrice([], Now);
        nothing.Should().Throw<DomainException>().Which.Code.Should().Be(PricingErrors.PriceNotAvailable);
    }

    [Theory]
    [InlineData(100_000L, 150_001L, true)]
    [InlineData(100_000L, 150_000L, false)]
    [InlineData(100_000L, 49_999L, true)]
    [InlineData(null, 1L, false)]
    public void Large_changes_are_flagged(long? previous, long next, bool expected)
        => PeriodSchedule.IsLargeChange(previous, next).Should().Be(expected);

    [Fact]
    public void Tax_rates_are_limited_to_2800_bp()
    {
        var act = () => PeriodFactory.PlanTaxRule([], "SEAFOOD_FRESH_CHILLED", 2801, Now, Now, false);
        act.Should().Throw<RefusalException>();
        PeriodFactory.PlanTaxRule([], "SEAFOOD_FRESH_CHILLED", 500, Now, Now, false).Added!.RateBp.Should().Be(500);
    }
}
