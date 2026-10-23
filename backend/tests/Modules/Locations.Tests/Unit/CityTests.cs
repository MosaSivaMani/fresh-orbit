using AwesomeAssertions;
using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Public.Events;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Locations.Tests.Unit;

public sealed class CityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Activate_draft_city_with_nothing_missing_makes_it_active_and_raises_event()
    {
        var city = NewCity();
        var by = Guid.CreateVersion7();

        city.Activate([], Now, by);

        city.Status.Should().Be(CityStatus.Active);
        city.ActivatedAt.Should().Be(Now);
        city.ActivatedBy.Should().Be(by);
        city.DomainEvents.OfType<CityActivated>().Should().ContainSingle().Which.CityId.Should().Be(city.Id);
    }

    [Fact]
    public void Activate_with_missing_items_lists_every_item()
    {
        var city = NewCity();
        var locationId = Guid.CreateVersion7();
        MissingItem[] missing =
        [
            new("SLOT_TEMPLATE_MISSING", locationId, "No active slot template."),
            new("PRICE_MISSING", locationId, "No variant has a current price."),
            new("STOCK_ITEM_MISSING", locationId, "No stock item exists."),
        ];

        var act = () => city.Activate(missing, Now, null);

        act.Should().Throw<CityNotReadyException>()
            .Which.Missing.Select(m => m.Code).Should().Equal("SLOT_TEMPLATE_MISSING", "PRICE_MISSING", "STOCK_ITEM_MISSING");
        city.Status.Should().Be(CityStatus.Draft);
    }

    [Fact]
    public void Activate_active_or_retired_city_is_an_invalid_transition()
    {
        var active = NewCity();
        active.Activate([], Now, null);
        var retired = NewCity();
        retired.Retire([]);

        foreach (var city in new[] { active, retired })
        {
            var act = () => city.Activate([], Now, null);
            act.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
        }
    }

    [Fact]
    public void Retire_active_city_is_refused_but_paused_city_retires()
    {
        var city = NewCity();
        city.Activate([], Now, null);

        var retireActive = () => city.Retire([]);
        retireActive.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");

        city.Pause();
        city.Retire([]);
        city.Status.Should().Be(CityStatus.Retired);
    }

    [Fact]
    public void Retire_with_blocking_items_is_refused()
    {
        var city = NewCity();

        var act = () => city.Retire(["OPEN_ORDERS_EXIST"]);

        act.Should().Throw<CityRetirementBlockedException>().Which.Blocking.Should().Equal("OPEN_ORDERS_EXIST");
        city.Status.Should().Be(CityStatus.Draft);
    }

    [Theory]
    [InlineData("chennai", "Chennai", "TN", "Asia/Kolkata", "code")]
    [InlineData("CHENNAI", "", "TN", "Asia/Kolkata", "name")]
    [InlineData("CHENNAI", "Chennai", "Tamil Nadu", "Asia/Kolkata", "stateCode")]
    [InlineData("CHENNAI", "Chennai", "TN", "Mars/Olympus", "timeZone")]
    public void Create_refuses_invalid_fields(string code, string name, string stateCode, string timeZone, string field)
    {
        var act = () => City.Create(code, name, stateCode, timeZone);

        act.Should().Throw<RefusalException>().Which.Errors.Should().Contain(e => e.Field == field && e.Code == "VALIDATION_FAILED");
    }

    [Fact]
    public void Readiness_lists_location_items_and_items_reported_by_checks()
    {
        var withoutArea = new LocationReadinessCounts(Guid.CreateVersion7(), 0, 0);
        var withoutPincode = new LocationReadinessCounts(Guid.CreateVersion7(), 1, 0);
        var ready = new LocationReadinessCounts(Guid.CreateVersion7(), 2, 5);

        var missing = CityReadiness.Evaluate([withoutArea, withoutPincode, ready], [new MissingItem("PRICE_MISSING", ready.FulfillmentLocationId, "No price.")]);

        missing.Should().BeEquivalentTo(
        [
            new { Code = "SERVICE_AREA_MISSING", FulfillmentLocationId = (Guid?)withoutArea.FulfillmentLocationId },
            new { Code = "PINCODE_MISSING", FulfillmentLocationId = (Guid?)withoutPincode.FulfillmentLocationId },
            new { Code = "PRICE_MISSING", FulfillmentLocationId = (Guid?)ready.FulfillmentLocationId },
        ]);
    }

    [Fact]
    public void Readiness_without_active_location_reports_it_for_the_city()
        => CityReadiness.Evaluate([], []).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Code = "FULFILLMENT_LOCATION_MISSING", FulfillmentLocationId = (Guid?)null });

    private static City NewCity() => City.Create("TEST_CITY", "Test City", "TN", "Asia/Kolkata");
}
