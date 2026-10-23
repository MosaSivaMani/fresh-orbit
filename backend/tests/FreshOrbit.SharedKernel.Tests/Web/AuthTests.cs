using System.Net;
using AwesomeAssertions;

namespace FreshOrbit.SharedKernel.Tests.Web;

public sealed class AuthTests(SharedKernelDatabase database) : ApiTestBase(database)
{
    private const string Url = "/api/v1/test/secure";

    [Fact]
    public async Task Missing_token_is_401_problem()
    {
        var response = await Factory.CreateClient().GetAsync(Url, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("UNAUTHENTICATED");
    }

    [Fact]
    public async Task Token_without_the_permission_is_403_problem()
    {
        var customer = await Factory.CreateCustomerClient().GetAsync(Url, Ct);
        var otherStaff = await Factory.CreateStaffClient(["orders.view"]).GetAsync(Url, Ct);

        customer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        otherStaff.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadJsonAsync(otherStaff)).GetProperty("code").GetString().Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Staff_with_the_permission_is_allowed_and_the_actor_is_read_from_claims()
    {
        var userId = Guid.CreateVersion7();

        var response = await Factory.CreateStaffClient(["orders.pack", "orders.view"], allLocations: true, userId: userId).GetAsync(Url, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var actor = await ReadJsonAsync(response);
        actor.GetProperty("actorType").GetString().Should().Be("STAFF");
        actor.GetProperty("userId").GetGuid().Should().Be(userId);
        actor.GetProperty("staffMemberId").GetGuid().Should().NotBeEmpty();
        actor.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should().Equal("orders.pack", "orders.view");
        actor.GetProperty("isAllLocations").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Expired_token_is_401()
    {
        Factory.Clock.Set(DateTimeOffset.UtcNow.AddHours(-2));
        var client = Factory.CreateStaffClient(["orders.pack"]);

        var response = await client.GetAsync(Url, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
