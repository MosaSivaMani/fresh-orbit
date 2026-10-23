using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Ordering.Infrastructure.Persistence;

/// <summary>The Ordering module's tables, all in the <c>ordering</c> schema.</summary>
public sealed class OrderingModuleModel : IModuleModel
{
    public const string SchemaName = "ordering";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderingModuleModel).Assembly);
    }
}
