using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Catalog.Infrastructure.Persistence;

/// <summary>The Catalog module's tables, all in the <c>catalog</c> schema.</summary>
public sealed class CatalogModuleModel : IModuleModel
{
    public const string SchemaName = "catalog";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogModuleModel).Assembly);
    }
}
