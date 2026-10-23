using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Inventory.Infrastructure.Persistence;

/// <summary>The Inventory module's tables, all in the <c>inventory</c> schema.</summary>
public sealed class InventoryModuleModel : IModuleModel
{
    public const string SchemaName = "inventory";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryModuleModel).Assembly);
    }
}
