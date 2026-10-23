using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Settings.Infrastructure.Persistence;

/// <summary>The Settings module's tables, all in the <c>settings</c> schema.</summary>
public sealed class SettingsModuleModel : IModuleModel
{
    public const string SchemaName = "settings";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SettingsModuleModel).Assembly);
    }
}
