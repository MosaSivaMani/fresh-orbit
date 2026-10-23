using AwesomeAssertions;
using FreshOrbit.Locations.Domain.FulfillmentLocations;
using FreshOrbit.Locations.Public.Events;
using FreshOrbit.Locations.Domain.ServiceAreas;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Locations.Tests.Unit;

public sealed class LocationAndAreaTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    [Fact]
    public void Location_cannot_become_active_without_FSSAI_number()
    {
        var location = NewLocation(fssai: null);
        location.SetOperatingHours([(DayOfWeek.Monday, new TimeOnly(7, 0), new TimeOnly(21, 0))]);

        var act = () => location.ChangeStatus(LocationStatus.Active, cityActive: false, cityRetired: false, otherActiveLocationsInCity: 0);

        act.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "fssaiLicenseNo");
        location.Status.Should().Be(LocationStatus.Draft);
    }

    [Fact]
    public void Location_activation_lists_every_missing_requirement()
    {
        var location = FulfillmentLocation.Create(Guid.CreateVersion7(), false, "CHN-X", new LocationDetails("X", null, null, null, null, null, false, null));

        var act = () => location.ChangeStatus(LocationStatus.Active, false, false, 0);

        act.Should().Throw<RefusalException>().Which.Errors.Select(e => e.Field)
            .Should().BeEquivalentTo(["addressLine", "pincode", "latitude", "fssaiLicenseNo", "hasColdStorage", "operatingHours"]);
    }

    [Fact]
    public void Pausing_the_last_active_location_of_an_active_city_is_refused()
    {
        var location = ActiveLocation();

        var act = () => location.ChangeStatus(LocationStatus.Paused, cityActive: true, cityRetired: false, otherActiveLocationsInCity: 0);

        act.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
        location.ChangeStatus(LocationStatus.Paused, cityActive: true, cityRetired: false, otherActiveLocationsInCity: 1);
        location.Status.Should().Be(LocationStatus.Paused);
        location.DomainEvents.OfType<FulfillmentLocationStatusChanged>().Last().To.Should().Be("PAUSED");
    }

    [Fact]
    public void Paused_location_cannot_go_back_to_draft()
    {
        var location = ActiveLocation();
        location.ChangeStatus(LocationStatus.Paused, false, false, 0);

        var act = () => location.ChangeStatus(LocationStatus.Draft, false, false, 0);

        act.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
    }

    [Fact]
    public void Free_delivery_below_minimum_order_is_refused()
    {
        var act = () => ServiceArea.Create(Guid.Empty, Guid.CreateVersion7(), Guid.Empty, "Adyar", new ServiceAreaFees(3900, 19900, 29900));

        act.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "freeDeliveryAbovePaise" && e.Code == "VALIDATION_FAILED");
    }

    [Fact]
    public void Service_area_location_must_be_in_the_same_city()
    {
        var act = () => ServiceArea.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Adyar", new ServiceAreaFees(0, null, 0));

        act.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "fulfillmentLocationId");
    }

    [Fact]
    public void Hours_must_close_after_they_open()
    {
        var location = NewLocation();

        var act = () => location.SetOperatingHours([(DayOfWeek.Monday, new TimeOnly(21, 0), new TimeOnly(7, 0)), (DayOfWeek.Tuesday, new TimeOnly(7, 0), new TimeOnly(7, 0))]);

        act.Should().Throw<RefusalException>().Which.Errors.Select(e => e.Field).Should().Equal("days[0].closesAt", "days[1].closesAt");
    }

    [Fact]
    public void Hours_replace_the_whole_week()
    {
        var location = NewLocation();
        location.SetOperatingHours([(DayOfWeek.Monday, new TimeOnly(7, 0), new TimeOnly(21, 0)), (DayOfWeek.Tuesday, new TimeOnly(7, 0), new TimeOnly(21, 0))]);

        location.SetOperatingHours([(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(20, 0))]);

        location.OperatingHours.Should().ContainSingle().Which.Should().BeEquivalentTo(new { DayOfWeek = DayOfWeek.Tuesday, OpensAt = new TimeOnly(8, 0) });
    }

    [Fact]
    public void Closure_in_the_past_is_refused_and_duplicates_conflict()
    {
        var location = NewLocation();

        var past = () => location.AddClosure(Today.AddDays(-1), "Holiday", Today);
        past.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "closedOn");

        location.AddClosure(Today, "Diwali", Today);
        var duplicate = () => location.AddClosure(Today, "Again", Today);
        duplicate.Should().Throw<ConflictException>().Which.Code.Should().Be("DUPLICATE");
    }

    [Fact]
    public void Only_future_closures_can_be_removed()
    {
        var location = NewLocation();
        var todays = location.AddClosure(Today, null, Today);
        var future = location.AddClosure(Today.AddDays(3), null, Today);

        var removeToday = () => location.RemoveClosure(todays.Id, Today);
        removeToday.Should().Throw<ConflictException>();
        location.RemoveClosure(future.Id, Today);

        location.Closures.Should().ContainSingle().Which.Id.Should().Be(todays.Id);
        location.DomainEvents.OfType<LocationClosureRemoved>().Should().ContainSingle();
    }

    [Theory]
    [InlineData("012345")]
    [InlineData("60004")]
    [InlineData("6000411")]
    [InlineData("60004A")]
    public void Malformed_pincodes_are_refused_by_index(string pincode)
    {
        var area = ServiceArea.Create(Guid.Empty, Guid.CreateVersion7(), Guid.Empty, "Adyar", new ServiceAreaFees(0, null, 0));

        var act = () => area.AddPincodes(["600041", pincode], new HashSet<string>());

        act.Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "pincodes[1]" && e.Code == "VALIDATION_FAILED");
        area.Pincodes.Should().BeEmpty();
    }

    [Fact]
    public void Pincode_active_elsewhere_conflicts_and_nothing_is_added()
    {
        var area = ServiceArea.Create(Guid.Empty, Guid.CreateVersion7(), Guid.Empty, "Adyar", new ServiceAreaFees(0, null, 0));

        var act = () => area.AddPincodes(["600041", "600020"], new HashSet<string> { "600020" });

        act.Should().Throw<ConflictException>().Which.Should().BeEquivalentTo(new { Code = "PINCODE_ALREADY_ASSIGNED", Field = "pincodes[1]" });
        area.Pincodes.Should().BeEmpty();
    }

    [Fact]
    public void Removed_pincode_keeps_history_and_can_be_added_again()
    {
        var area = ServiceArea.Create(Guid.Empty, Guid.CreateVersion7(), Guid.Empty, "Adyar", new ServiceAreaFees(0, null, 0));
        area.AddPincodes(["600041"], new HashSet<string>());

        area.RemovePincode("600041");
        area.AddPincodes(["600041"], new HashSet<string>());

        area.Pincodes.Should().HaveCount(2);
        area.Pincodes.Count(p => p.IsActive).Should().Be(1);
    }

    private static FulfillmentLocation NewLocation(string? fssai = "12345678901234")
        => FulfillmentLocation.Create(Guid.CreateVersion7(), false, "CHN-TVM", new LocationDetails("Thiruvanmiyur", "1 Beach Road", "600041", "+914400000000", 12.98m, 80.26m, true, fssai));

    private static FulfillmentLocation ActiveLocation()
    {
        var location = NewLocation();
        location.SetOperatingHours([(DayOfWeek.Monday, new TimeOnly(7, 0), new TimeOnly(21, 0))]);
        location.ChangeStatus(LocationStatus.Active, false, false, 0);
        return location;
    }
}
