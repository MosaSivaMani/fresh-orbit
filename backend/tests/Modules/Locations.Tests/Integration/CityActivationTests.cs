using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Locations.Tests.Integration;

public sealed class CityActivationTests(LocationsDatabase database) : LocationsTestBase(database)
{
    [Fact]
    public async Task Activation_refusal_lists_every_missing_item_with_location_ids()
    {
        var setup = await CreateServedSetupAsync();
        Factory.Checks.Missing.Add(new CityMissingItem("SLOT_TEMPLATE_MISSING", setup.LocationId, "No active slot template."));
        Factory.Checks.Missing.Add(new CityMissingItem("STOCK_ITEM_MISSING", setup.LocationId, "No stock item exists."));
        var version = await VersionOfAsync($"/api/v1/admin/cities/{setup.CityId}");

        var response = await SendAsync(Admin, HttpMethod.Post, $"/api/v1/admin/cities/{setup.CityId}/activate", ifMatch: version);
        var json = await ReadJsonAsync(response);
        var city = await ReadJsonAsync(await Admin.GetAsync($"/api/v1/admin/cities/{setup.CityId}", Ct));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        json.GetProperty("code").GetString().Should().Be("CITY_NOT_READY");
        var missing = json.GetProperty("missing").EnumerateArray().ToList();
        missing.Select(m => m.GetProperty("code").GetString()).Should().Equal("SLOT_TEMPLATE_MISSING", "STOCK_ITEM_MISSING");
        missing.Should().OnlyContain(m => m.GetProperty("fulfillmentLocationId").GetGuid() == setup.LocationId);
        city.GetProperty("status").GetString().Should().Be("DRAFT");
        city.GetProperty("readiness").GetProperty("isReady").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Readiness_lists_own_items_for_a_city_without_service_area()
    {
        var city = await PostOkAsync("/api/v1/admin/cities", new { code = UniqueCode("CITY"), name = "Empty", stateCode = "KA", timeZone = "Asia/Kolkata" });

        var readiness = await ReadJsonAsync(await Admin.GetAsync($"/api/v1/admin/cities/{city.GetProperty("id").GetGuid()}/readiness", Ct));

        readiness.GetProperty("isReady").GetBoolean().Should().BeFalse();
        readiness.GetProperty("missing").EnumerateArray().Select(m => m.GetProperty("code").GetString()).Should().Equal("FULFILLMENT_LOCATION_MISSING");
    }

    [Fact]
    public async Task Ready_city_activates_and_writes_the_event_to_the_outbox()
    {
        var setup = await CreateServedSetupAsync();
        var version = await VersionOfAsync($"/api/v1/admin/cities/{setup.CityId}");

        var response = await SendAsync(Admin, HttpMethod.Post, $"/api/v1/admin/cities/{setup.CityId}/activate", ifMatch: version);
        var json = await ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.GetProperty("status").GetString().Should().Be("ACTIVE");
        response.Headers.ETag!.Tag.Should().NotBe(version);
        await using var db = Database.CreateDbContext();
        (await db.Set<OutboxMessage>().Where(m => m.EventType.Contains("CityActivated")).ToListAsync(Ct))
            .Should().Contain(m => m.Payload.Contains(setup.CityId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Activate_with_stale_version_is_412_and_without_version_is_428()
    {
        var setup = await CreateServedSetupAsync();
        var version = await VersionOfAsync($"/api/v1/admin/cities/{setup.CityId}");
        await SendOkAsync(HttpMethod.Put, $"/api/v1/admin/cities/{setup.CityId}", new { name = "Renamed", stateCode = "TN", timeZone = "Asia/Kolkata" }, version.Trim('"'));

        var stale = await SendAsync(Admin, HttpMethod.Post, $"/api/v1/admin/cities/{setup.CityId}/activate", ifMatch: version);
        var missing = await SendAsync(Admin, HttpMethod.Post, $"/api/v1/admin/cities/{setup.CityId}/activate");

        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        (await CodeOfAsync(stale)).Should().Be("CONCURRENCY_CONFLICT");
        missing.StatusCode.Should().Be((HttpStatusCode)428);
        (await CodeOfAsync(missing)).Should().Be("VERSION_REQUIRED");
    }

    [Fact]
    public async Task Retire_with_open_work_is_refused_and_status_unchanged()
    {
        var setup = await CreateServedSetupAsync();
        Factory.Checks.Blocking.Add("OPEN_ORDERS_EXIST");
        var version = await VersionOfAsync($"/api/v1/admin/cities/{setup.CityId}");

        var response = await SendAsync(Admin, HttpMethod.Post, $"/api/v1/admin/cities/{setup.CityId}/retire", ifMatch: version);
        var json = await ReadJsonAsync(response);
        var city = await ReadJsonAsync(await Admin.GetAsync($"/api/v1/admin/cities/{setup.CityId}", Ct));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        json.GetProperty("code").GetString().Should().Be("CITY_RETIREMENT_BLOCKED");
        json.GetProperty("blocking").EnumerateArray().Select(b => b.GetString()).Should().Equal("OPEN_ORDERS_EXIST");
        city.GetProperty("status").GetString().Should().Be("DRAFT");
    }

    [Fact]
    public async Task Closure_writes_its_event_to_the_outbox_in_the_same_save()
    {
        var setup = await CreateServedSetupAsync();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        var response = await Admin.PostAsJsonAsync($"/api/v1/admin/fulfillment-locations/{setup.LocationId}/closures", new { closedOn = date, reason = "Festival" }, Ct);
        var duplicate = await Admin.PostAsJsonAsync($"/api/v1/admin/fulfillment-locations/{setup.LocationId}/closures", new { closedOn = date, reason = "Again" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOfAsync(duplicate)).Should().Be("DUPLICATE");
        await using var db = Database.CreateDbContext();
        (await db.Set<OutboxMessage>().Where(m => m.EventType.Contains("LocationClosureAdded")).ToListAsync(Ct))
            .Should().Contain(m => m.Payload.Contains(setup.LocationId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Duplicate_city_code_is_409()
    {
        var code = UniqueCode("CITY");
        await PostOkAsync("/api/v1/admin/cities", new { code, name = "One", stateCode = "TN", timeZone = "Asia/Kolkata" });

        var response = await Admin.PostAsJsonAsync("/api/v1/admin/cities", new { code, name = "Two", stateCode = "TN", timeZone = "Asia/Kolkata" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).Should().Be("DUPLICATE");
    }

    [Fact]
    public async Task Permissions_are_enforced()
    {
        var setup = await CreateServedSetupAsync();
        var manager = Factory.CreateStaffClient(["locations.manage"], allLocations: true);
        var packer = Factory.CreateStaffClient(["orders.pack"], allLocations: true);
        var customer = Factory.CreateCustomerClient();
        var scoped = Factory.CreateStaffClient(["locations.manage"], locationIds: [Guid.CreateVersion7()]);
        var version = await VersionOfAsync($"/api/v1/admin/cities/{setup.CityId}");

        var activateWithoutPermission = await SendAsync(manager, HttpMethod.Post, $"/api/v1/admin/cities/{setup.CityId}/activate", ifMatch: version);
        var listAsPacker = await packer.GetAsync("/api/v1/admin/cities", Ct);
        var listAsCustomer = await customer.GetAsync("/api/v1/admin/cities", Ct);
        var anonymous = await Factory.CreateClient().GetAsync("/api/v1/admin/cities", Ct);
        var otherLocation = await scoped.GetAsync($"/api/v1/admin/fulfillment-locations/{setup.LocationId}", Ct);
        var scopedList = await ReadJsonAsync(await scoped.GetAsync("/api/v1/admin/fulfillment-locations", Ct));

        activateWithoutPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        listAsPacker.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        listAsCustomer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        otherLocation.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOfAsync(otherLocation)).Should().Be("LOCATION_ACCESS_DENIED");
        scopedList.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Public_city_list_shows_only_active_cities()
    {
        var draft = await CreateServedSetupAsync();
        var active = await CreateServedSetupAsync();
        await SendOkAsync(HttpMethod.Post, $"/api/v1/admin/cities/{active.CityId}/activate", null, (await VersionOfAsync($"/api/v1/admin/cities/{active.CityId}")).Trim('"'));

        var cities = await ReadJsonAsync(await Factory.CreateClient().GetAsync("/api/v1/cities", Ct));

        var ids = cities.EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
        ids.Should().Contain(active.CityId).And.NotContain(draft.CityId);
    }
}
