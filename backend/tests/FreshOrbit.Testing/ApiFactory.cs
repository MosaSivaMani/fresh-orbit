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
/// <see cref="TestClock"/>. Create clients for a customer or a staff member with given permissions.
/// Override <see cref="ConfigureTestServices"/> to add test modules or replace services.
/// </summary>
public class ApiFactory(PostgresFixture database) : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-only-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";

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
        foreach (var policy in new[] { "Checkout", "Refresh", "Search" })
        {
            builder.UseSetting($"RateLimiting:{policy}:PermitLimit", "100000");
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
