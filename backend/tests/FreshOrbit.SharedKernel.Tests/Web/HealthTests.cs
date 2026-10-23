using System.Net;
using AwesomeAssertions;

namespace FreshOrbit.SharedKernel.Tests.Web;

public sealed class HealthTests(SharedKernelDatabase database) : ApiTestBase(database)
{
    [Fact]
    public async Task Health_reports_healthy_when_the_database_is_reachable()
    {
        var response = await Factory.CreateClient().GetAsync("/health", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Be("Healthy");
    }
}
