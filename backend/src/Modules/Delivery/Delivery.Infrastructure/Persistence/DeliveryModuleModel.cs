using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Delivery.Infrastructure.Persistence;

/// <summary>The Delivery module's tables, all in the <c>delivery</c> schema.</summary>
public sealed class DeliveryModuleModel : IModuleModel
{
    public const string SchemaName = "delivery";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeliveryModuleModel).Assembly);
    }
}
