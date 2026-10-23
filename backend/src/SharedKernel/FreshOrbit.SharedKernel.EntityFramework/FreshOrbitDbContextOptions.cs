using FreshOrbit.SharedKernel.EntityFramework.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FreshOrbit.SharedKernel.EntityFramework;

/// <summary>The one place that configures the Npgsql provider for every host, the design-time factory and tests.</summary>
public static class FreshOrbitDbContextOptions
{
    /// <summary>EF migrations live in the Migrations host.</summary>
    public const string MigrationsAssembly = "FreshOrbit.Migrations";

    public const string ConnectionStringName = "FreshOrbit";

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        builder
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(MigrationsAssembly)
                .MigrationsHistoryTable("__ef_migrations_history", PlatformModuleModel.SchemaName))
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IModelCacheKeyFactory, FreshOrbitModelCacheKeyFactory>();
        return builder;
    }

    public static DbContextOptions<FreshOrbitDbContext> Build(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<FreshOrbitDbContext>();
        Configure(builder, connectionString);
        return builder.Options;
    }
}
