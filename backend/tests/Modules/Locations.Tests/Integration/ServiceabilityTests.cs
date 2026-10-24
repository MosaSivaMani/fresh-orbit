using System.Net;
using AwesomeAssertions;
using FreshOrbit.Locations.Domain.ServiceAreas;
using FreshOrbit.Locations.Public;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FreshOrbit.Locations.Tests.Integration;

public sealed class ServiceabilityTests(LocationsDatabase database) : LocationsTestBase(database)
{
    [Fact]
    public async Task Active_pincode_resolves_to_the_full_location_context()
    {
        var setup = await CreateActiveCityAsync();

        var response = await Factory.CreateClient().GetAsync($"/api/v1/serviceability?pincode={setup.Pincode}", Ct);
        var json = await ReadJsonAsync(response);
        await using var scope = Factory.Services.CreateAsyncScope();
        var resolved = await scope.ServiceProvider.GetRequiredService<ILocationsApi>().ResolvePincodeAsync(setup.Pincode, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.GetProperty("serviceable").GetBoolean().Should().BeTrue();
        json.GetProperty("city").GetProperty("id").GetGuid().Should().Be(setup.CityId);
        json.GetProperty("city").GetProperty("name").GetString().Should().Be("Test City");
        json.GetProperty("city").GetProperty("timeZone").GetString().Should().Be("Asia/Kolkata");
        json.GetProperty("fulfillmentLocation").GetProperty("id").GetGuid().Should().Be(setup.LocationId);
        json.GetProperty("serviceArea").GetProperty("deliveryFeePaise").GetInt64().Should().Be(3900);
        json.GetProperty("serviceArea").GetProperty("minOrderPaise").GetInt64().Should().Be(29900);
        json.GetProperty("nextSlot").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        resolved.Should().BeEquivalentTo(new
        {
            IsServiceable = true,
            CityId = (Guid?)setup.CityId,
            FulfillmentLocationId = (Guid?)setup.LocationId,
            ServiceAreaId = (Guid?)setup.ServiceAreaId,
            TimeZone = "Asia/Kolkata",
            DeliveryFeePaise = (long?)3900,
            FreeDeliveryAbovePaise = (long?)49900,
            MinOrderPaise = (long?)29900,
        });
        resolved.FulfillmentLocationCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Next_slot_carries_its_local_date_and_cutoff_as_the_delivery_eta()
    {
        var setup = await CreateActiveCityAsync();
        var slotId = Guid.CreateVersion7();
        // 20:00 UTC is 01:30 the next day in Asia/Kolkata: the date must be the location's, not UTC's.
        var startAt = new DateTimeOffset(2030, 3, 4, 20, 0, 0, TimeSpan.Zero);
        var cutoffAt = startAt.AddHours(-2);
        Factory.NextSlots.Slots[setup.LocationId] = new NextSlot(slotId, startAt, startAt.AddHours(2), cutoffAt);

        var json = await ReadJsonAsync(await Factory.CreateClient().GetAsync($"/api/v1/serviceability?pincode={setup.Pincode}", Ct));

        var nextSlot = json.GetProperty("nextSlot");
        nextSlot.GetProperty("id").GetGuid().Should().Be(slotId);
        nextSlot.GetProperty("startAt").GetDateTimeOffset().Should().Be(startAt);
        nextSlot.GetProperty("endAt").GetDateTimeOffset().Should().Be(startAt.AddHours(2));
        nextSlot.GetProperty("date").GetString().Should().Be("2030-03-05");
        nextSlot.GetProperty("cutoffAt").GetDateTimeOffset().Should().Be(cutoffAt);
    }

    [Fact]
    public async Task Unknown_pincode_answers_200_not_serviceable_and_malformed_is_422()
    {
        var client = Factory.CreateClient();

        var unknown = await client.GetAsync("/api/v1/serviceability?pincode=999999", Ct);
        var malformed = await client.GetAsync("/api/v1/serviceability?pincode=012345", Ct);

        unknown.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonAsync(unknown);
        json.GetProperty("serviceable").GetBoolean().Should().BeFalse();
        json.GetProperty("reasonCode").GetString().Should().Be("PINCODE_NOT_SERVICEABLE");
        malformed.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOfAsync(malformed)).Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Draft_city_does_not_serve_its_pincodes()
    {
        var setup = await CreateServedSetupAsync();

        var json = await ReadJsonAsync(await Factory.CreateClient().GetAsync($"/api/v1/serviceability?pincode={setup.Pincode}", Ct));

        json.GetProperty("serviceable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Inactive_area_paused_location_or_paused_city_stop_resolution()
    {
        var client = Factory.CreateClient();

        var area = await CreateActiveCityAsync();
        var areaJson = await ReadJsonAsync(await Admin.GetAsync($"/api/v1/admin/service-areas/{area.ServiceAreaId}", Ct));
        await SendOkAsync(HttpMethod.Put, $"/api/v1/admin/service-areas/{area.ServiceAreaId}", new { name = "Adyar", deliveryFeePaise = 3900, freeDeliveryAbovePaise = 49900, minOrderPaise = 29900, isActive = false }, areaJson.GetProperty("version").GetString());

        var city = await CreateActiveCityAsync();
        await SendOkAsync(HttpMethod.Post, $"/api/v1/admin/cities/{city.CityId}/pause", null, (await VersionOfAsync($"/api/v1/admin/cities/{city.CityId}")).Trim('"'));

        var location = await CreateActiveCityAsync();
        await PauseCityAndLocationAsync(location);

        foreach (var pincode in new[] { area.Pincode, city.Pincode, location.Pincode })
        {
            var json = await ReadJsonAsync(await client.GetAsync($"/api/v1/serviceability?pincode={pincode}", Ct));
            json.GetProperty("serviceable").GetBoolean().Should().BeFalse($"pincode {pincode} must stop resolving");
        }
    }

    [Fact]
    public async Task Pincode_active_elsewhere_is_refused_and_nothing_is_inserted()
    {
        var first = await CreateServedSetupAsync();
        var second = await CreateServedSetupAsync();
        var fresh = NextPincode();
        var version = await VersionOfAsync($"/api/v1/admin/service-areas/{second.ServiceAreaId}");

        var response = await SendAsync(Admin, HttpMethod.Post, $"/api/v1/admin/service-areas/{second.ServiceAreaId}/pincodes", new { pincodes = new[] { fresh, first.Pincode } }, version);
        var json = await ReadJsonAsync(response);
        var pincodes = await ReadJsonAsync(await Admin.GetAsync($"/api/v1/admin/service-areas/{second.ServiceAreaId}/pincodes", Ct));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        json.GetProperty("code").GetString().Should().Be("PINCODE_ALREADY_ASSIGNED");
        json.GetProperty("errors").TryGetProperty("pincodes[1]", out _).Should().BeTrue();
        pincodes.EnumerateArray().Select(p => p.GetProperty("pincode").GetString()).Should().Equal(second.Pincode);
    }

    [Fact]
    public async Task Unique_index_is_the_last_line_for_concurrent_assignment()
    {
        var first = await CreateServedSetupAsync();
        var second = await CreateServedSetupAsync();
        var pincode = NextPincode();
        await using var db1 = Database.CreateDbContext();
        await using var db2 = Database.CreateDbContext();
        var area1 = await db1.Set<ServiceArea>().Include(a => a.Pincodes).FirstAsync(a => a.Id == first.ServiceAreaId, Ct);
        var area2 = await db2.Set<ServiceArea>().Include(a => a.Pincodes).FirstAsync(a => a.Id == second.ServiceAreaId, Ct);

        // Both commands passed their own check (nothing active elsewhere when they read).
        area1.AddPincodes([pincode], new HashSet<string>());
        area2.AddPincodes([pincode], new HashSet<string>());
        await db1.SaveChangesAsync(Ct);
        var second2 = () => db2.SaveChangesAsync(Ct);

        (await second2.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>().Which.ConstraintName.Should().Be("ux_pincodes_active_pincode");
    }

    private async Task<ServedSetup> CreateActiveCityAsync()
    {
        var setup = await CreateServedSetupAsync();
        await SendOkAsync(HttpMethod.Post, $"/api/v1/admin/cities/{setup.CityId}/activate", null, (await VersionOfAsync($"/api/v1/admin/cities/{setup.CityId}")).Trim('"'));
        return setup;
    }

    private async Task PauseCityAndLocationAsync(ServedSetup setup)
    {
        // The last active location of an ACTIVE city cannot be paused (LOC-R12): open a second one first.
        await CreateActiveLocationAsync(setup.CityId);
        var location = await ReadJsonAsync(await Admin.GetAsync($"/api/v1/admin/fulfillment-locations/{setup.LocationId}", Ct));
        await SetLocationStatusAsync(setup.LocationId, "PAUSED", location.GetProperty("version").GetString()!);
    }
}
