using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Locations.Infrastructure.Persistence;

/// <summary>The Locations module's tables, all in the <c>locations</c> schema.</summary>
public sealed class LocationsModuleModel : IModuleModel
{
    public const string SchemaName = "locations";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocationsModuleModel).Assembly);
    }
}
