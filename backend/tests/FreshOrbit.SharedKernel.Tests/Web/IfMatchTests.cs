using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;

namespace FreshOrbit.SharedKernel.Tests.Web;

public sealed class IfMatchTests(SharedKernelDatabase database) : ApiTestBase(database)
{
    [Fact]
    public async Task Update_requires_the_current_version_in_If_Match()
    {
        var client = Factory.CreateClient();
        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/test/widgets", new { name = "versioned" }, Ct));
        var url = $"/api/v1/test/widgets/{created.GetProperty("id").GetGuid()}";
        var etag = (await client.GetAsync(url, Ct)).Headers.ETag!.Tag;

        var missing = await SendPutAsync(client, url, null);
        var current = await SendPutAsync(client, url, etag);
        var stale = await SendPutAsync(client, url, etag);
        var unreadable = await SendPutAsync(client, url, "\"abc\"");

        missing.StatusCode.Should().Be((HttpStatusCode)428);
        (await ReadJsonAsync(missing)).GetProperty("code").GetString().Should().Be("VERSION_REQUIRED");
        current.StatusCode.Should().Be(HttpStatusCode.OK);
        current.Headers.ETag!.Tag.Should().NotBe(etag);
        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        (await ReadJsonAsync(stale)).GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");
        unreadable.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<HttpResponseMessage> SendPutAsync(HttpClient client, string url, string? ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(new { name = "renamed" }) };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await client.SendAsync(request, Ct);
    }
}
