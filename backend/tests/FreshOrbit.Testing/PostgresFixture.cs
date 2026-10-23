using Microsoft.EntityFrameworkCore;
using FreshOrbit.Hosting;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Time;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace FreshOrbit.Testing;

/// <summary>
/// A real PostgreSQL 17 (Testcontainers, <c>postgres:17-alpine</c>) with the schema of every module built by
/// <c>EnsureCreatedAsync</c>. Use one per test assembly (<c>[assembly: AssemblyFixture(typeof(...))]</c>).
/// Derive and override <see cref="AdditionalModuleModels"/> to add test-only tables.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("freshorbit")
        .WithUsername("freshorbit")
        .WithPassword("freshorbit_test")
        .Build();

    private IReadOnlyList<IModuleModel> _moduleModels = [];

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>The platform model, every module's model and <see cref="AdditionalModuleModels"/>.</summary>
    public IReadOnlyList<IModuleModel> ModuleModels => _moduleModels;

    protected virtual IEnumerable<IModuleModel> AdditionalModuleModels => [];

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        var services = new ServiceCollection();
        services.AddModuleModel<PlatformModuleModel>();
        services.AddFreshOrbitModules(new ConfigurationBuilder().Build(), Modules.All);
        await using (var provider = services.BuildServiceProvider())
        {
            _moduleModels = [.. provider.GetServices<IModuleModel>(), .. AdditionalModuleModels];
        }

        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        foreach (var sql in _moduleModels.SelectMany(model => model.DatabaseObjectsSql))
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }
    }

    /// <summary>A context on the test database (system actor and a fresh <see cref="TestClock"/> by default).</summary>
    public FreshOrbitDbContext CreateDbContext(ICurrentActor? actor = null, IClock? clock = null)
        => new(FreshOrbitDbContextOptions.Build(ConnectionString), ModuleModels, actor ?? TestActor.System(), clock ?? new TestClock());

    /// <summary>
    /// A service provider wired like the Worker (persistence, outbox dispatcher, test clock and actor) against the
    /// test database; <paramref name="configure"/> adds handlers or overrides.
    /// </summary>
    public ServiceProvider CreateServices(Action<IServiceCollection>? configure = null, TestClock? clock = null, ICurrentActor? actor = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"ConnectionStrings:{FreshOrbitDbContextOptions.ConnectionStringName}"] = ConnectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton<IClock>(clock ?? new TestClock());
        services.AddSingleton(actor ?? TestActor.System());
        services.AddFreshOrbitPersistence();
        foreach (var model in ModuleModels)
        {
            services.AddSingleton(model);
        }

        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
