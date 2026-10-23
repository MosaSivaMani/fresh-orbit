using FreshOrbit.Identity.Infrastructure.Seeding;
using FreshOrbit.Notifications.Infrastructure.Seeding;
using FreshOrbit.Settings.Infrastructure.Seeding;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.E1Testing;

/// <summary>
/// The test database with the reference data of Identity (roles, permissions), Settings (default order policy,
/// number series) and Notifications (templates) loaded once. Shared by the E1 module test projects.
/// </summary>
public sealed class E1Database : PostgresFixture, IAsyncLifetime
{
    public static string ReferenceDataPath
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "data", "reference");
                if (Directory.Exists(Path.Combine(candidate, "identity")))
                {
                    return candidate;
                }
            }

            throw new DirectoryNotFoundException("backend/data/reference was not found above the test output directory.");
        }
    }

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await base.InitializeAsync();
        await LoadReferenceDataAsync();
    }

    /// <summary>Runs the loaders (idempotent, so tests may call it again).</summary>
    public async Task LoadReferenceDataAsync()
    {
        await using var provider = CreateServices(services =>
        {
            services.AddScoped<IReferenceDataLoader, IdentityReferenceDataLoader>();
            services.AddScoped<IReferenceDataLoader, SettingsReferenceDataLoader>();
            services.AddScoped<IReferenceDataLoader, NotificationsReferenceDataLoader>();
        });
        await using var scope = provider.CreateAsyncScope();
        foreach (var loader in scope.ServiceProvider.GetServices<IReferenceDataLoader>().OrderBy(l => l.Order))
        {
            await loader.LoadAsync(new ReferenceDataContext(ReferenceDataPath), CancellationToken.None);
        }
    }
}
