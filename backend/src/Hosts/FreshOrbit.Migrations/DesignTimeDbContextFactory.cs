using FreshOrbit.Hosting;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Time;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.EntityFrameworkCore.Design;

namespace FreshOrbit.Migrations;

/// <summary>
/// Used by <c>dotnet ef migrations add &lt;Name&gt; --project backend/src/Hosts/FreshOrbit.Migrations</c>.
/// The model is the platform model plus every module in <see cref="Modules.All"/>. The connection string comes from
/// <c>ConnectionStrings__FreshOrbit</c> or appsettings, else the local dev database.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FreshOrbitDbContext>
{
    private const string LocalConnectionString = "Host=localhost;Port=5432;Database=freshorbit;Username=freshorbit;Password=freshorbit_dev";

    public FreshOrbitDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString(FreshOrbitDbContextOptions.ConnectionStringName);

        var services = new ServiceCollection();
        services.AddModuleModel<PlatformModuleModel>();
        services.AddFreshOrbitModules(configuration, Modules.All);
        using var provider = services.BuildServiceProvider();

        return new FreshOrbitDbContext(
            FreshOrbitDbContextOptions.Build(string.IsNullOrWhiteSpace(connectionString) ? LocalConnectionString : connectionString),
            provider.GetServices<IModuleModel>().ToList(),
            SystemActor.Instance,
            new SystemClock());
    }
}
