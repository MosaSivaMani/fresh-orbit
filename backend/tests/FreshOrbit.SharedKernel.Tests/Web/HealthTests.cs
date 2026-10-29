using System.Net;
using AwesomeAssertions;
using FreshOrbit.Settings.Domain.BusinessProfiles;
using Microsoft.EntityFrameworkCore;

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

    [Fact]
    public async Task Readiness_is_unhealthy_until_the_business_profile_holds_what_invoicing_needs()
    {
        // BE-1 / D295. This test database has no business profile (no test of this assembly writes one but this).
        using var client = Factory.CreateClient();

        (await client.GetAsync("/health/ready", Ct)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await client.GetAsync("/health", Ct)).StatusCode.Should().Be(HttpStatusCode.OK, "liveness does not depend on the store configuration");

        const string first14 = "33AAACF1234A1Z";
        await using (var db = Database.CreateDbContext())
        {
            db.Add(BusinessProfile.Create(new BusinessProfileData("Fresh Orbit Foods Pvt Ltd", "Fresh Orbit", null, null, "Chennai", "+914400000000", "care@example.com", "Asia/Kolkata")));
            await db.SaveChangesAsync(Ct);
        }

        try
        {
            (await client.GetAsync("/health/ready", Ct)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "the GSTIN is still missing");

            await using (var db = Database.CreateDbContext())
            {
                var profile = await db.Set<BusinessProfile>().SingleAsync(Ct);
                profile.Update(new BusinessProfileData("Fresh Orbit Foods Pvt Ltd", "Fresh Orbit", first14 + Gstins.CheckCharacter(first14), null, "Chennai", "+914400000000", "care@example.com", "Asia/Kolkata"));
                await db.SaveChangesAsync(Ct);
            }

            var ready = await client.GetAsync("/health/ready", Ct);
            ready.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ready.Content.ReadAsStringAsync(Ct)).Should().Be("Healthy");
        }
        finally
        {
            await using var cleanup = Database.CreateDbContext();
            await cleanup.Set<BusinessProfile>().ExecuteDeleteAsync(Ct);
        }
    }
}
