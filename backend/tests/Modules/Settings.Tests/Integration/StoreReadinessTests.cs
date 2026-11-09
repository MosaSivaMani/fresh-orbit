using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.E1Testing;
using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.FulfillmentLocations;
using FreshOrbit.Notifications.Public;
using FreshOrbit.Settings.Domain.BusinessProfiles;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Settings.Tests.Integration;

/// <summary>D358 / SET-R10: <c>GET /api/v1/admin/settings/store-readiness</c> composes what blocks real trading.</summary>
public sealed class StoreReadinessTests(E1Database database) : E1ApiTestBase(database)
{
    private const string Url = "/api/v1/admin/settings/store-readiness";

    [Fact]
    public async Task Readiness_needs_settings_manage()
    {
        using var other = Factory.CreateStaffClient(["orders.view"]);
        (await other.GetAsync(Url, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Factory.CreateClient().GetAsync(Url, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Development_senders_are_reported_missing_with_their_provider_code_only()
    {
        using var admin = Factory.CreateStaffClient(["settings.manage"]);

        var response = await admin.GetAsync(Url, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("isReady").GetBoolean().Should().BeFalse("the Testing host uses the development senders");
        var channels = body.GetProperty("channels").EnumerateArray().ToList();
        channels.Select(c => c.GetProperty("channel").GetString()).Should().Equal("SMS", "EMAIL", "PUSH");
        foreach (var channel in channels)
        {
            channel.GetProperty("provider").GetString().Should().Be("DEVELOPMENT");
            channel.GetProperty("status").GetString().Should().Be("MISSING");
            channel.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["channel", "provider", "status"], "no setting value is ever returned");
        }

        body.GetProperty("businessProfile").EnumerateArray().Select(f => f.GetProperty("field").GetString())
            .Should().Equal("legalName", "address", "gstin", "timeZone");
    }

    [Fact]
    public async Task Store_is_ready_only_when_profile_every_location_licence_and_every_channel_are_ok()
    {
        Factory.AdditionalServices = services => services.Replace(ServiceDescriptor.Singleton<INotificationProviders>(new RealProviders()));
        using var admin = Factory.CreateStaffClient(["settings.manage"]);
        await SaveCompleteProfileAsync(admin);
        try
        {
            await AssertReadinessAsync(admin);
        }
        finally
        {
            // The assembly shares one database: leave no business profile behind for SettingsApiTests.
            await InScopeAsync(async provider =>
            {
                var db = provider.GetRequiredService<FreshOrbitDbContext>();
                return await db.Set<BusinessProfile>().ExecuteDeleteAsync(Ct);
            });
        }
    }

    private async Task AssertReadinessAsync(HttpClient admin)
    {
        var (licensedId, unlicensedId) = await SeedLocationsAsync();

        var before = await ReadJsonAsync(await admin.GetAsync(Url, Ct));

        before.GetProperty("isReady").GetBoolean().Should().BeFalse("one location has no FSSAI licence");
        before.GetProperty("businessProfile").EnumerateArray().Should().OnlyContain(f => f.GetProperty("status").GetString() == "OK");
        before.GetProperty("channels").EnumerateArray().Select(c => (c.GetProperty("provider").GetString(), c.GetProperty("status").GetString()))
            .Should().Equal(("DLT", "OK"), ("SMTP", "OK"), ("FCM", "OK"));
        var locations = before.GetProperty("locations").EnumerateArray().ToDictionary(l => l.GetProperty("locationId").GetGuid());
        locations[licensedId].GetProperty("fssaiLicenseStatus").GetString().Should().Be("OK");
        locations[licensedId].GetProperty("locationStatus").GetString().Should().Be("DRAFT");
        locations[unlicensedId].GetProperty("fssaiLicenseStatus").GetString().Should().Be("MISSING");

        await InScopeAsync(async provider =>
        {
            var db = provider.GetRequiredService<FreshOrbitDbContext>();
            foreach (var location in await db.Set<FulfillmentLocation>().Where(l => l.FssaiLicenseNo == null || l.FssaiLicenseNo == string.Empty).ToListAsync(Ct))
            {
                location.Update(new LocationDetails(location.Name, location.AddressLine, location.Pincode, location.Phone, location.Latitude, location.Longitude, location.HasColdStorage, "10012345000999"));
            }

            return await db.SaveChangesAsync(Ct);
        });

        var after = await ReadJsonAsync(await admin.GetAsync(Url, Ct));
        after.GetProperty("isReady").GetBoolean().Should().BeTrue();
        after.GetProperty("locations").EnumerateArray().Should().OnlyContain(l => l.GetProperty("fssaiLicenseStatus").GetString() == "OK");
    }

    private static async Task SaveCompleteProfileAsync(HttpClient admin)
    {
        const string first14 = "33AAACF1234A1Z";
        var body = new
        {
            legalName = "Fresh Orbit Private Limited",
            tradeName = "Fresh Orbit",
            gstin = first14 + Gstins.CheckCharacter(first14),
            fssaiLicenseNo = (string?)null,
            address = "1 Anna Salai, Chennai 600002",
            supportPhone = "+914412345678",
            supportEmail = "support@freshorbit.in",
            timeZone = "Asia/Kolkata",
        };
        var current = await admin.GetAsync("/api/v1/admin/settings/business-profile", Ct);
        using var save = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/settings/business-profile") { Content = JsonContent.Create(body) };
        if (current.StatusCode == HttpStatusCode.OK)
        {
            save.Headers.IfMatch.Add(current.Headers.ETag!);
        }
        else
        {
            save.Headers.IfNoneMatch.ParseAdd("*");
        }

        var saved = await admin.SendAsync(save, Ct);
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(Ct));
    }

    private async Task<(Guid Licensed, Guid Unlicensed)> SeedLocationsAsync()
    {
        var suffix = Guid.CreateVersion7().ToString("N")[^6..].ToUpperInvariant();
        return await InScopeAsync(async provider =>
        {
            var db = provider.GetRequiredService<FreshOrbitDbContext>();
            var city = City.Create($"READY_{suffix}", "Readiness City", "TN", "Asia/Kolkata");
            var licensed = FulfillmentLocation.Create(city.Id, false, $"RDY-{suffix}-A", new LocationDetails("Licensed", "1 Beach Road", "600041", "+914400000000", 12.98m, 80.26m, true, "10012345000123"));
            var unlicensed = FulfillmentLocation.Create(city.Id, false, $"RDY-{suffix}-B", new LocationDetails("Unlicensed", null, null, null, null, null, false, null));
            db.AddRange(city, licensed, unlicensed);
            await db.SaveChangesAsync(Ct);
            return (licensed.Id, unlicensed.Id);
        });
    }

    private sealed class RealProviders : INotificationProviders
    {
        public NotificationProvidersStatus GetStatus() => new(
            new ChannelProviderStatus("DLT", true),
            new ChannelProviderStatus("SMTP", true),
            new ChannelProviderStatus("FCM", true));
    }
}
