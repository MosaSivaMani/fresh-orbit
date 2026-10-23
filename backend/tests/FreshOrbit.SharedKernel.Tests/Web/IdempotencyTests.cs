using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.SharedKernel.Tests.TestModule;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.SharedKernel.Tests.Web;

public sealed class IdempotencyTests(SharedKernelDatabase database) : ApiTestBase(database)
{
    [Fact]
    public async Task Same_key_and_request_replays_the_stored_response_without_running_twice()
    {
        var client = Factory.CreateCustomerClient();
        var key = Guid.NewGuid().ToString();
        var name = $"idem-{key}";

        var first = await PostAsync(client, key, name);
        var second = await PostAsync(client, key, name);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        second.Headers.GetValues("Idempotent-Replayed").Should().Equal("true");
        second.Headers.Location.Should().Be(first.Headers.Location);
        (await ReadJsonAsync(second)).GetProperty("id").GetGuid().Should().Be((await ReadJsonAsync(first)).GetProperty("id").GetGuid());
        await using var db = Database.CreateDbContext();
        (await db.Set<Widget>().CountAsync(w => w.Name == name, Ct)).Should().Be(1);
    }

    [Fact]
    public async Task Same_key_with_a_different_request_is_refused()
    {
        var client = Factory.CreateCustomerClient();
        var key = Guid.NewGuid().ToString();
        await PostAsync(client, key, "first-body");

        var reused = await PostAsync(client, key, "second-body");

        reused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadJsonAsync(reused)).GetProperty("code").GetString().Should().Be("IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task Keys_are_scoped_per_caller()
    {
        var key = Guid.NewGuid().ToString();
        var name = $"scoped-{key}";

        var first = await PostAsync(Factory.CreateCustomerClient(), key, name);
        var second = await PostAsync(Factory.CreateCustomerClient(), key, name);

        second.Headers.Contains("Idempotent-Replayed").Should().BeFalse();
        (await ReadJsonAsync(second)).GetProperty("id").GetGuid().Should().NotBe((await ReadJsonAsync(first)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Refused_request_releases_the_key()
    {
        var client = Factory.CreateCustomerClient();
        var key = Guid.NewGuid().ToString();

        var refused = await PostAsync(client, key, string.Empty);
        var retried = await PostAsync(client, key, "fixed-name");

        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        retried.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string key, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/test/widgets") { Content = JsonContent.Create(new { name }) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Ct);
    }
}
