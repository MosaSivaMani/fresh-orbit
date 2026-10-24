using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Locations.Infrastructure.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Locations.Tests.Integration;

/// <summary>Staff lookups G1 (own locations), G2 (cities) and the typed CITY_RETIREMENT_BLOCKED problem (G11).</summary>
public sealed class StaffLookupTests(LocationsDatabase database) : LocationsTestBase(database)
{
    [Fact]
    public async Task Me_locations_lists_only_the_callers_locations_for_any_staff()
    {
        var first = await CreateServedSetupAsync();
        var second = await CreateServedSetupAsync();
        using var packer = Factory.CreateStaffClient(["orders.pack"], [first.LocationId]);

        var page = await GetJsonAsync(packer, "/api/v1/admin/me/locations?pageSize=100");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Should().ContainSingle();
        items[0].GetProperty("id").GetGuid().Should().Be(first.LocationId);
        items[0].GetProperty("cityId").GetGuid().Should().Be(first.CityId);
        items[0].GetProperty("code").GetString().Should().StartWith("LOC-");
        items[0].GetProperty("name").GetString().Should().Be("Test Store");
        items[0].GetProperty("status").GetString().Should().Be("ACTIVE");
        page.GetProperty("totalCount").GetInt32().Should().Be(1);
        page.GetProperty("page").GetInt32().Should().Be(1);
        items.Select(i => i.GetProperty("id").GetGuid()).Should().NotContain(second.LocationId);
    }

    [Fact]
    public async Task Me_locations_lists_every_location_for_all_locations_staff()
    {
        var setup = await CreateServedSetupAsync();
        using var manager = Factory.CreateStaffClient(["reports.view"], allLocations: true);

        var first = await GetJsonAsync(manager, "/api/v1/admin/me/locations?pageSize=1");
        var all = await AllPagesAsync(manager, "/api/v1/admin/me/locations?");

        first.GetProperty("items").GetArrayLength().Should().Be(1);
        first.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        all.Select(i => i.GetProperty("id").GetGuid()).Should().Contain(setup.LocationId);
    }

    [Fact]
    public async Task Lookups_need_a_staff_token()
    {
        using var anonymous = Factory.CreateClient();
        using var customer = Factory.CreateCustomerClient();

        (await anonymous.GetAsync("/api/v1/admin/me/locations", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await customer.GetAsync("/api/v1/admin/me/locations", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.GetAsync("/api/v1/admin/cities/lookup", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await customer.GetAsync("/api/v1/admin/cities/lookup", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task City_lookup_is_readable_by_staff_without_locations_manage_and_filters_by_status()
    {
        var setup = await CreateServedSetupAsync();
        using var support = Factory.CreateStaffClient(["customers.view"]);

        (await support.GetAsync("/api/v1/admin/cities", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the full city list stays locations.manage");
        var drafts = await AllPagesAsync(support, "/api/v1/admin/cities/lookup?status=DRAFT");
        var city = drafts.Single(c => c.GetProperty("id").GetGuid() == setup.CityId);
        city.GetProperty("code").GetString().Should().StartWith("CITY_");
        city.GetProperty("name").GetString().Should().Be("Test City");
        city.GetProperty("status").GetString().Should().Be("DRAFT");
        drafts.Should().OnlyContain(c => c.GetProperty("status").GetString() == "DRAFT");

        var active = await AllPagesAsync(support, "/api/v1/admin/cities/lookup?status=ACTIVE");
        active.Select(c => c.GetProperty("id").GetGuid()).Should().NotContain(setup.CityId);

        var badPage = await support.GetAsync("/api/v1/admin/cities/lookup?pageSize=500", Ct);
        badPage.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Retirement_blocked_problem_has_the_documented_shape()
    {
        var setup = await CreateServedSetupAsync();
        Factory.Checks.Blocking.AddRange(["ORDERS_OPEN", "SLOTS_BOOKED"]);
        var version = await VersionOfAsync($"/api/v1/admin/cities/{setup.CityId}");

        var response = await SendAsync(Admin, HttpMethod.Post, $"/api/v1/admin/cities/{setup.CityId}/retire", ifMatch: version);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var json = await ReadJsonAsync(response);
        json.GetProperty("code").GetString().Should().Be("CITY_RETIREMENT_BLOCKED");
        json.GetProperty("status").GetInt32().Should().Be(422);
        json.GetProperty("blocking").EnumerateArray().Select(b => b.GetString()).Should().Equal("ORDERS_OPEN", "SLOTS_BOOKED");
        json.GetProperty("errors").ValueKind.Should().Be(JsonValueKind.Object);
        json.GetProperty("errors").EnumerateObject().Should().BeEmpty();

        // Every member the published schema names is written (camelCase), so the spec matches the runtime.
        var documented = typeof(CityRetirementBlockedProblem).GetProperties().Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name));
        var required = documented.Except(["instance", "type"]);
        json.EnumerateObject().Select(p => p.Name).Should().Contain(required);
    }

    [Fact]
    public void Retire_endpoint_publishes_the_typed_problem_in_the_spec()
    {
        var endpoint = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/api/v1/admin/cities/{cityId:guid}/retire");

        endpoint.Metadata.OfType<IProducesResponseTypeMetadata>()
            .Should().Contain(m => m.StatusCode == StatusCodes.Status422UnprocessableEntity
                && m.Type == typeof(CityRetirementBlockedProblem)
                && m.ContentTypes.Contains("application/problem+json"));
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        var json = await ReadJsonAsync(response);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url} answered {json}");
        return json;
    }

    /// <summary>Every item of a paged list (the database is shared by the assembly; never assume page 1).</summary>
    private static async Task<List<JsonElement>> AllPagesAsync(HttpClient client, string url)
    {
        var items = new List<JsonElement>();
        for (var page = 1; ; page++)
        {
            var json = await GetJsonAsync(client, $"{url}&pageSize=100&page={page}");
            items.AddRange(json.GetProperty("items").EnumerateArray());
            if (items.Count >= json.GetProperty("totalCount").GetInt32() || json.GetProperty("items").GetArrayLength() == 0)
            {
                return items;
            }
        }
    }
}
