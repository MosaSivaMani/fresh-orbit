using System.Net.Http.Headers;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Time;
using FreshOrbit.SharedKernel.Web.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Testing;

/// <summary>
/// The real Api host (<c>Program</c>) on the Testcontainers database, with a test JWT signing key and a
/// <see cref="TestClock"/> and generous rate limits (override <c>RateLimiting:&lt;Rule&gt;</c> to test one). Create clients for a customer or a staff member with given permissions.
/// Override <see cref="ConfigureTestServices"/> to add test modules or replace services.
/// </summary>
public class ApiFactory(PostgresFixture database) : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-only-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";

    /// <summary>Every rate-limit rule of the Api (section <c>RateLimiting</c>); tests raise them because they share one client IP.</summary>
    public static readonly IReadOnlyList<string> RateLimitRules = ["OtpRequest", "OtpVerify", "Login", "Refresh", "Checkout", "Search", "Public", "Coupon", "Upload"];

    /// <summary>The rules that also give each signed-in caller its own budget (<c>RateLimiting:&lt;Rule&gt;:PerUserPermitLimit</c>).</summary>
    public static readonly IReadOnlyList<string> UserPartitionedRateLimitRules = ["Search", "Public"];

    public TestClock Clock { get; } = new();

    public HttpClient CreateClientFor(AccessTokenSubject subject)
    {
        var client = CreateClient();
        var token = Services.GetRequiredService<AccessTokenFactory>().Create(subject).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateCustomerClient(Guid? userId = null, Guid? customerId = null)
        => CreateClientFor(new AccessTokenSubject(userId ?? Guid.CreateVersion7(), ActorType.Customer, CustomerId: customerId ?? Guid.CreateVersion7()));

    public HttpClient CreateStaffClient(IReadOnlyCollection<string> permissions, IReadOnlyCollection<Guid>? locationIds = null, bool allLocations = false, Guid? userId = null)
        => CreateClientFor(new AccessTokenSubject(
            userId ?? Guid.CreateVersion7(),
            ActorType.Staff,
            StaffMemberId: Guid.CreateVersion7(),
            Permissions: permissions,
            LocationIds: locationIds,
            AllLocations: allLocations));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{FreshOrbitDbContextOptions.ConnectionStringName}", database.ConnectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
        // Required outside Development and Testing (E2E-3); set here so tests that start the Api as Production start.
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(Path.GetTempPath(), "freshorbit-api-tests", "dataprotection-keys"));
        foreach (var rule in RateLimitRules)
        {
            builder.UseSetting($"RateLimiting:{rule}:PermitLimit", "100000");
        }

        foreach (var rule in UserPartitionedRateLimitRules)
        {
            builder.UseSetting($"RateLimiting:{rule}:PerUserPermitLimit", "100000");
        }

        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IClock>(Clock));
            foreach (var model in database.ModuleModels)
            {
                services.AddSingleton(model);
            }

            ConfigureTestServices(services);
        });
    }

    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }
}
