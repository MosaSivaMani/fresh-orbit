using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Locations.Public;
using FreshOrbit.Locations.Tests.Integration;
using FreshOrbit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: AssemblyFixture(typeof(LocationsDatabase))]

namespace FreshOrbit.Locations.Tests.Integration;

public sealed class LocationsDatabase : PostgresFixture;

/// <summary>Activation / retirement checks whose answers a test sets.</summary>
public sealed class FakeCityChecks : ICityActivationCheck, ICityRetirementCheck
{
    public List<CityMissingItem> Missing { get; } = [];

    public List<string> Blocking { get; } = [];

    public Task<IReadOnlyList<string>> GetMissingItemsAsync(Guid cityId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([.. Missing.Select(m => m.Code)]);

    public Task<IReadOnlyList<CityMissingItem>> GetMissingDetailsAsync(Guid cityId, IReadOnlyCollection<Guid> activeLocationIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CityMissingItem>>([.. Missing]);

    public Task<IReadOnlyList<string>> GetBlockingItemsAsync(Guid cityId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([.. Blocking]);
}

/// <summary>The real Api with the other modules' city checks replaced by <see cref="FakeCityChecks"/>.</summary>
public sealed class LocationsApiFactory(LocationsDatabase database) : ApiFactory(database)
{
    public FakeCityChecks Checks { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<ICityActivationCheck>();
        services.RemoveAll<ICityRetirementCheck>();
        services.AddSingleton<ICityActivationCheck>(Checks);
        services.AddSingleton<ICityRetirementCheck>(Checks);
    }
}

public abstract class LocationsTestBase(LocationsDatabase database) : IAsyncLifetime
{
    private static int _pincodeSeed = Random.Shared.Next(100_000, 800_000);

    protected LocationsApiFactory Factory { get; } = new(database);

    protected LocationsDatabase Database => database;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected HttpClient Admin => _admin ??= Factory.CreateStaffClient(["locations.manage", "cities.activate"], allLocations: true);

    private HttpClient? _admin;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        _admin?.Dispose();
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected static string NextPincode() => Interlocked.Increment(ref _pincodeSeed).ToString(System.Globalization.CultureInfo.InvariantCulture);

    protected static string UniqueCode(string prefix) => $"{prefix}_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).Clone();

    protected static async Task<string> CodeOfAsync(HttpResponseMessage response) => (await ReadJsonAsync(response)).GetProperty("code").GetString()!;

    protected static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, object? body = null, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue(ifMatch));
        }

        return await client.SendAsync(request, Ct);
    }

    /// <summary>A city with one ACTIVE location and an active service area serving one pincode (city still DRAFT).</summary>
    protected async Task<ServedSetup> CreateServedSetupAsync()
    {
        var city = await PostOkAsync("/api/v1/admin/cities", new { code = UniqueCode("CITY"), name = "Test City", stateCode = "TN", timeZone = "Asia/Kolkata" });
        var cityId = city.GetProperty("id").GetGuid();
        var locationId = await CreateActiveLocationAsync(cityId);
        var area = await PostOkAsync("/api/v1/admin/service-areas", new { cityId, fulfillmentLocationId = locationId, name = "Adyar", deliveryFeePaise = 3900, freeDeliveryAbovePaise = 49900, minOrderPaise = 29900 });
        var areaId = area.GetProperty("id").GetGuid();
        var pincode = NextPincode();
        await SendOkAsync(HttpMethod.Post, $"/api/v1/admin/service-areas/{areaId}/pincodes", new { pincodes = new[] { pincode } }, area.GetProperty("version").GetString());
        return new ServedSetup(cityId, locationId, areaId, pincode);
    }

    /// <summary>An ACTIVE fulfillment location (all LOC-R11 fields and Monday hours) in the city.</summary>
    protected async Task<Guid> CreateActiveLocationAsync(Guid cityId)
    {
        var location = await PostOkAsync("/api/v1/admin/fulfillment-locations", new
        {
            cityId,
            code = UniqueCode("LOC").Replace('_', '-'),
            name = "Test Store",
            addressLine = "1 Beach Road",
            pincode = "600041",
            phone = "+914400000000",
            latitude = 12.98m,
            longitude = 80.26m,
            hasColdStorage = true,
            fssaiLicenseNo = "12345678901234",
        });
        var locationId = location.GetProperty("id").GetGuid();
        var withHours = await SendOkAsync(HttpMethod.Put, $"/api/v1/admin/fulfillment-locations/{locationId}/operating-hours", new
        {
            days = new[] { new { dayOfWeek = "MONDAY", opensAt = "07:00", closesAt = "21:00" } },
        }, location.GetProperty("version").GetString());
        await SetLocationStatusAsync(locationId, "ACTIVE", withHours.GetProperty("version").GetString()!);
        return locationId;
    }

    protected Task<JsonElement> SetLocationStatusAsync(Guid locationId, string status, string version)
        => SendOkAsync(HttpMethod.Put, $"/api/v1/admin/fulfillment-locations/{locationId}", new
        {
            name = "Test Store",
            addressLine = "1 Beach Road",
            pincode = "600041",
            phone = "+914400000000",
            latitude = 12.98m,
            longitude = 80.26m,
            hasColdStorage = true,
            fssaiLicenseNo = "12345678901234",
            status,
        }, version);

    protected async Task<JsonElement> PostOkAsync(string url, object body)
    {
        var response = await Admin.PostAsJsonAsync(url, body, Ct);
        var json = await ReadJsonAsync(response);
        response.IsSuccessStatusCode.Should().BeTrue($"POST {url} answered {json}");
        return json;
    }

    protected async Task<JsonElement> SendOkAsync(HttpMethod method, string url, object? body, string? ifMatch)
    {
        var response = await SendAsync(Admin, method, url, body, ifMatch is null ? null : $"\"{ifMatch}\"");
        var json = await ReadJsonAsync(response);
        response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} answered {json}");
        return json;
    }

    protected async Task<string> VersionOfAsync(string url)
    {
        var response = await Admin.GetAsync(url, Ct);
        response.IsSuccessStatusCode.Should().BeTrue();
        return response.Headers.ETag!.Tag;
    }

    protected sealed record ServedSetup(Guid CityId, Guid LocationId, Guid ServiceAreaId, string Pincode);
}
